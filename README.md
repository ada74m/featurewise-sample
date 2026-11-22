# SampleService

A minimal service built with Featurewise, and the starting point for a new one. Its domain is deliberately blank: a list of Things, which signed-in people can rename and editors can delete, and which a schedule puts back every night. It runs as three AWS Lambda functions, one per entrypoint, over one core. Replace Things with whatever your service is about.

`PRINCIPLES.md` says how every featurewise service is built, this one included. A new service copies it along with the code.

## Building

The service uses Featurewise as two packages from nuget.org, at the version `Directory.Build.props` names: the core references `Featurewise.Core`, which has what features, links and rules are written with, and each entrypoint references `Featurewise.Hosting`, which registers and invokes them. The core can't see `Features`, so it can't invoke a feature.

```
dotnet test
```

To change Featurewise and the service together, build against a local checkout of featurewise instead of the package: `dotnet test -p:FeaturewiseSource=../library`, or set `FeaturewiseSource` in the environment.

## Features

| Feature | Family | What it does |
|---|---|---|
| `Things` | Query | Every Thing, each with a link to delete it and a link to rename it, offered only when the caller may follow them. |
| `DeleteThing` | Command | Deletes a Thing, if the caller is an editor (its rule, nested in it), and leads back to `Things`. |
| `RenameThing` | Command | Renames a Thing. `Things` offers its link with `NewName` null, and invoking it that way returns `NeedsNewName`, with the link back, so a UI asks for the name and follows the link with it. |
| `HandleRestoreDue` | Worker | Puts the Things back as they started: deleted ones return, renamed ones get their names back, and any others go. It runs nightly, and once when the service is first deployed. |

## Projects

| Project | What it is |
|---|---|
| `SampleService` | The core: `Models/` (`Thing`), `Features/`, each with its rule nested in it when it has one of its own, and `Facilities/` (`IThingsPersistence`, and `IChanges`, which commits each invocation's writes together). It references only `Featurewise.Core`, and nothing of AWS. |
| `SampleService.Aws` | The facilities on AWS (`DynamoThingsPersistence`, and `DynamoChanges`, which commits as one DynamoDB transaction), and the registrations every function uses. |
| `SampleService.Local` | The whole service in one process, over facilities in memory (`InMemoryThings`, `InMemoryThingsPersistence` and `InMemoryChanges`), so it can be used without deploying it. See "Running locally". |
| `SampleService.UiBackend` | The backend for the service's own UI, on API Gateway's HTTP API. Its contract is the features themselves, links and result cases included, so it serves only a UI shipped with the service. A link names its feature in a property that says its family, qualified by the service, as in `"$command":"sample/DeleteThing"`, so a UI shell combining several services' modules knows which service's backend to send it to, and how. `GET /{feature}?link=<the link's JSON>` invokes a query, which a reload or Back may repeat. `POST /{feature}` invokes a command with its link as the JSON body; a GET for a command is answered with 405, so no reload runs one again. A query may be posted too. The backend's URL names the service and feature, so a body may leave the feature out, but mustn't name another, or name it under another family. The caller comes from the JWT authorizer's claims, and an `Idempotency-Key` header becomes the invocation's idempotency key. A result is `application/json`, and an error is an RFC 9457 problem, `application/problem+json`, so the UI shell tells them apart by content type. |
| `SampleService.PublicApi` | The API for other organisations' systems, on API Gateway's HTTP API. Its contract is its own, in `openapi.yaml`: versioned resources (`GET /v1/things`), HTTP status codes for outcomes, RFC 9457 problems for errors, and public shapes without links or result cases. A client may repeat a GET, so a GET route invokes only a query. Clients authenticate with OAuth client credentials from a Cognito user pool of their own, and the rule for each feature checks the token's scopes. |
| `SampleService.ScheduledWorker` | EventBridge Scheduler. Each schedule's input carries its job's link, so one function runs every job, as a `SystemCaller` named after the schedule. |
| `SampleService.Ui` | The service's module for the Featurewise UI shell: a React project, built by Vite into one ES module, with the shell's module builder, `featurewiseModule`, from the npm package `@featurewise/ui`. It gives `Things` the address `things`, and handles three result cases itself: `DeleteThing`'s `Deleted` and `RenameThing`'s `Renamed` show a toast and go back to the list, and `RenameThing`'s `NeedsNewName` is a form asking for the name. The shell's generic renderer shows everything else. On AWS, the package uploads it to the UI shell's bucket under `modules/sample/`, when the environment passes in `shell_bucket`. |
| `SampleService.Tests` | Tests of the core's features, of each entrypoint fed the events AWS would send it, and of the local host. |

Each entrypoint registers only the features it invokes, each with the rules that say who may invoke it there.

Most services have one implementation of each facility and one place to run, and keep their implementations in the core. The sample splits them out because its readers run it in different places: on AWS, and on a laptop.

## Running locally

```
dotnet run --project SampleService.Local --urls http://localhost:5000
```

To use it in a browser, build the shell and the module first. The shell is `featurewise-ui`, checked out beside this repository, as `../ui`:

```
(cd ../ui && npm install && npm run build)
(cd SampleService.Ui && npm install && npm run build)
```

Then open http://localhost:5000. The pill in the top right corner signs you in as Ann, an editor, or Bob, who may rename but not delete. The settings `ShellFiles` and `ModuleFiles` point the host at other builds.

The host makes each request into the event API Gateway or EventBridge would send the deployed function, and hands it to the same `Function` class, so what runs locally is what's deployed:

| Path | Entrypoint |
|---|---|
| `POST /api/sample/{feature}`, `GET /api/sample/{feature}?link=` | The UI backend, at the path the UI shell will reach it by. |
| `GET /public/sample/v1/things` | The public API's routes, under `/public/sample`. |
| `POST /jobs` | Runs the job whose link is the body, as a schedule would, such as `{"$worker":"sample/HandleRestoreDue"}`. |
| `GET /services.json` | The shell's configuration: the service's module and backend, and the users to sign in as. |
| `GET /modules/sample/...` | The module, from `SampleService.Ui/dist`. |
| Any other `GET` | The shell, from `../ui/dist`, which reads the address from the URL. |

The Things are kept in memory and restored when the host starts. A caller signs in with a bearer token the host reads without checking, so a developer can be anyone. It needs the claims API Gateway's authorizer would pass on, such as Ann the editor's:

```
token="none.$(printf '{"username":"ann","cognito:groups":["editor"]}' | base64).none"
curl -X POST localhost:5000/api/sample/Things -H "Authorization: Bearer $token"
```

## Infrastructure

Each deployed project keeps the Terraform for its own resources in its `Infrastructure` folder, as a module: `SampleService.Aws` the things table, and each function its own. `Infrastructure/` here is the service's module, which calls them and passes one's outputs to another, such as the table's name to each function. The functions share `Infrastructure/modules/lambda-function`. The service's module has no provider or state of its own: an environment supplies those, with the inputs that differ between environments, such as the issuer of its users' tokens.

The UI backend has no CORS configuration, so a browser can call it only from the backend's own origin. The plan is for the estate's UI shell to serve itself and every service's UI backend from one CloudFront distribution, routing `/api/sample/*` to this backend with the prefix stripped. The browser then makes only same-origin requests. That needs no CORS and no preflight request for each feature's URL, and the shell finds each service's backend by a fixed path instead of each API's generated URL. If the shell called the backend from another origin instead, the API would need a `cors_configuration` that allows `POST` and `GET` and the `authorization`, `content-type` and `idempotency-key` headers from the shell's origin.

## Packages and CI

`package.sh <version> <out-dir>` builds the service's package: `sample-<version>.zip`, holding the Terraform modules in their places and each function's zip in `functions/`. An environment uses it as a module, `source = "s3::https://…/sample/<version>.zip//Infrastructure"`; the `//` makes Terraform fetch the whole package, so the modules find each other and the functions.

The `CI` workflow (`.github/workflows/ci.yml`) builds the solution with warnings as errors, runs the tests, checks the Terraform, and builds a package, for every pull request and push to main. Each push to main also publishes the package to the estate's artifacts bucket as `sample/<yyyy.mm.dd>.<run>.zip` and tags the commit `sample/<version>`, once the repository has the variable `AWS_REGION` and the secrets `ARTIFACTS_BUCKET` and `PUBLISH_ROLE_ARN` from the estate's bootstrap; they're secrets so that the logs don't show the account they name. Environments, each pinning the services' versions, live in a separate repository, which deploys them.

## Deploying

Real environments deploy from the environments repository. For your own stack, from your working tree, you need AWS credentials, Terraform, the .NET SDK, the AWS CLI and jq. `deploy.sh` builds a package into `out/`, as CI does, then runs `terraform apply` in `DevEnvironment/`, which uses the unpacked package and passes on its arguments:

```
./deploy.sh -var name_prefix=adam
```

`name_prefix` starts every resource's name, which the service follows with its id, as in `adam-sample-ui-backend`, so each developer can have a stack of their own; `terraform destroy` in `DevEnvironment` removes it. Its state is local.

A dev user pool stands in for the estate's sign-in, with the people in `dev_users`: by default Ann, an editor, and Bob, who isn't one, each with a generated password. To call the UI backend as Ann:

```
cd DevEnvironment
password=$(terraform output -json dev_user_passwords | jq -r .ann)
token=$(aws cognito-idp initiate-auth --auth-flow USER_PASSWORD_AUTH \
  --client-id "$(terraform output -raw dev_ui_client_id)" \
  --auth-parameters "$(jq -n --arg password "$password" '{USERNAME: "ann", PASSWORD: $password}')" \
  --query AuthenticationResult.AccessToken --output text)

curl -X POST "$(terraform output -raw ui_backend_url)/Things" -H "Authorization: Bearer $token"
```

`public_api_clients` lists the public API's third-party clients and their scopes. A client trades its id and secret for a token at the token URL:

```
client=$(terraform output -json public_api_clients | jq '.["example-partner"]')
token=$(curl -s -u "$(jq -r .client_id <<<"$client"):$(jq -r .client_secret <<<"$client")" \
  -d 'grant_type=client_credentials&scope=sample/things.read' \
  "$(terraform output -raw public_api_token_url)" | jq -r .access_token)

curl "$(terraform output -raw public_api_url)/v1/things" -H "Authorization: Bearer $token"
```

The public API throttles all clients together; quotas per client would need API keys on a REST API, or WAF rules.

## Licence

MIT: see `LICENSE`.
