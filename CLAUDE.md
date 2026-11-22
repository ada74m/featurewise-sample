# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```
export GITHUB_PACKAGES_TOKEN=$(gh auth token)   # restoring Featurewise's packages needs a token that can read packages
dotnet build                                     # build the solution (SampleService.sln)
dotnet test                                      # run all tests
dotnet build -p:FeaturewiseSource=../library   # build against a local featurewise checkout instead
./package.sh <version> out                       # build the service's package into out/
terraform fmt -check -recursive .
```

A change is done when the build is warning-free and the full test run passes. For a behaviour change, temporarily remove the new code to confirm its test fails. Check Terraform with `terraform fmt` and, in `Infrastructure` (and in `DevEnvironment` after `./package.sh dev out`), `terraform init -backend=false && terraform validate`. Never `plan` or `apply` against AWS unasked.

## What this is

The `sample` service: a minimal blank-domain service on AWS Lambda, the starting point for new services, built with Featurewise (from the `featurewise` repo, on the owner's GitHub Packages feed; its version is in `Directory.Build.props`). The core references only `Featurewise.Core`, and nothing of AWS; the entrypoints also reference `Featurewise.Hosting`, which holds `Features`. **Read `PRINCIPLES.md` before changing code: it says how every featurewise service is built, and the code follows it.** `README.md` explains this service, its features, its entrypoints and how to deploy it. Featurewise's own README explains the library.

- `SampleService` is the core. `Models/` holds domain models (`Thing`), `Features/` the features (`Things` a query, `DeleteThing` and `RenameThing` commands, `HandleRestoreDue` a worker), each with its rule nested in it when it has one of its own, and `Facilities/` whatever else supports the features (`IThingsPersistence`, and `IChanges`, the invocation's unit of work: persistence stages writes, and each command or worker commits them once, with conditions such as "still there" turning a lost race into `NotFound`). All share the `SampleService` namespace. `Service.Id` (`sample`) qualifies the feature every link names, in a property that says its family: `$query`, `$command` or `$worker`.
- `SampleService.Aws` holds the facilities' AWS implementations (`DynamoThingsPersistence`, and `DynamoChanges`, which commits as one `TransactWriteItems` with the idempotency key as its token), `Registrations.Register` that every function's default constructor uses, and the table's Terraform. `SampleService.Local` is an ASP.NET Core host that runs the whole service over in-memory facilities (`InMemoryThings`, the shared store, with `InMemoryThingsPersistence` and `InMemoryChanges` per invocation, which the tests use too; `InMemoryChanges` throws if an invocation never commits what it staged), turning each HTTP request into the Lambda event the deployed function gets and calling the same `Function`; it reads bearer tokens without verifying them (`UnsignedJwt`). Splitting implementations out of the core is the sample's exception, because readers run it on AWS and locally; most services wouldn't.
- One project per entrypoint, each registering only the features it invokes: `UiBackend` for the estate's UI shell (`GET /{feature}?link=` for a query, asking `Features.IsQuery`, and `POST /{feature}` for a command, or a query; results with links and cases as `application/json`, errors as `application/problem+json`), `PublicApi` for third parties (contract in `openapi.yaml`, kept in step by a test; GET routes invoke only queries) and `ScheduledWorker`.
- `SampleService.Ui` is the service's module for the UI shell (`featurewise-ui`, checked out at `../ui`): React, built by Vite into `dist/index.js`, with the shell's module builder, `featurewiseModule`, and contract types from the npm package `@ada74m/featurewise-ui` (the owner's GitHub Packages registry, named in its `.npmrc`; restoring needs `GITHUB_PACKAGES_TOKEN`, as the .NET packages do). `src/index.tsx` wires every feature it dresses: `Things`' address, and the renderers in `src/features/`, a file per feature, each named after the result case it renders (`Deleted`, `Renamed`, `NeedsNewName`); the shell's generic renderer shows the rest. `npm test` and `npm run build` there. The local host serves it under `/modules/sample/`, the shell from `../ui/dist` at every other path, and `services.json`. On AWS, `package.sh` builds it into the package, and `SampleService.Ui/Infrastructure` uploads it to the shell's bucket when the environment gives the service module a `shell_bucket`; without one, the service deploys with no UI, as `DevEnvironment` does.
- The sample's history, from before it was squashed to one commit to go public, including the HR service it used to be (tag `hr-final`, with a queue worker and events), is in the private `ada74m/featurewise-sample-history`.
- `SampleService.Tests` tests the features, drives each entrypoint with fake Lambda events over the in-memory store, and drives the local host over HTTP.

## Infrastructure and deployment

Each project keeps its Terraform in its own `Infrastructure/` module; the root `Infrastructure/` is the service's module, which wires them together and has no provider or state. `package.sh` builds the service's package (the modules plus `functions/*.zip`), which environments use as `s3::https://s3-<region>.amazonaws.com/<bucket>/sample/<version>.zip//Infrastructure`. `.github/workflows/ci.yml` builds, tests, checks Terraform and packages on every PR and push; pushes to main publish the package to the estate's artifacts bucket, versioned `<yyyy.mm.dd>.<run>`, and tag the commit. Real environments pin versions in a separate, private environments repository, checked out at `../environments` where present. `DevEnvironment/` is a developer's own root over a locally built package, deployed by `deploy.sh`.

## Conventions

- `PRINCIPLES.md` holds the rules for features, links, rules, Core, entrypoints, idempotency, contracts and tests. It's the file a new service copies, so keep it free of anything particular to this one beyond its examples.
- One type per file. A facility's interface is in the core's `Facilities/`; its implementations are in the project for where they run (`SampleService.Aws`, `SampleService.Local`).
- AWS resources are named `<environment>-<service>-<what it is>`: the environment passes `name_prefix`, and the root `Infrastructure/main.tf` adds the service's id (`local.service`, which must match `Service.Id`) before passing it to each project's module. The UI shell's are `<environment>-ui-…`, and the environment's own, such as its user pool, `<environment>-…`.
- Prose (README, comments) is in classic style: direct, no metadiscourse, no hedging.
