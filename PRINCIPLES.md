# Principles

How every featurewise service is built. This file barely varies from one service to the next; what a particular service does belongs in its service document. The rules are prescriptive on purpose. Each one comes with its reason, so that a person can see why, and so that an agent can tell what the rule protects when a case isn't covered.

The examples come from this sample: a blank domain of `Thing`s, with the features `Things`, `DeleteThing`, `RenameThing` and `HandleRestoreDue`.

## Features

A *feature* is something the service does that all its stakeholders would recognise. It does a whole job, not part of one: writing to the database isn't a feature, and nor is applying a validation rule. When a feature finishes, nothing is left half done.

The features are the service. Everything else in the code plays a supporting role. So the list of features is the complete description of what the service can do, and a change to what it does is a change to a feature, or a new one.

- **One feature, one class,** in `Features/`, named after the feature.
- **Its arguments are a record named `Args`, nested in it.** `Args` derives from `Link<TFeature, TResult>`: it's the feature's link (see "Links").
- **Its result is nested in it too,** as `Result`, unless it's an existing type.
- **Its rule is nested in it,** as `Rule`, when it has a rule of its own (see "Who may invoke").

### Families

Every feature belongs to one of three families, and derives from that family's base class. The family says whether the feature changes state, and whether anyone is waiting for its result.

| Family | Base class | Changes state | Somebody waiting | Example |
|---|---|---|---|---|
| Query | `Query<TArgs, TResult>` | No | Yes | `Things` |
| Command | `Command<TArgs, TResult>` | Yes | Yes | `DeleteThing`, `RenameThing` |
| Worker | `Worker<TArgs>` | Yes | No | `HandleRestoreDue` |

By "state" we mean anything a stakeholder would care about. Storing a value counts, but so does sending an email or publishing an event. Writing a log line doesn't.

- **A query changes nothing.** It's safe to invoke any number of times, so entrypoints may retry it, cache it, and invoke it by GET. The compiler can't check this; review does. A query may still have to record something a stakeholder cares about, such as who read a patient's record. It stays a query as long as repeating it does no harm.
- **A command's result is short.** It says what happened: "renamed", or "not found". Anyone who wants to see the new state follows a link to a query.
- **Anything someone might wait for is a command,** even if a job sometimes invokes it. A worker is a feature nobody ever waits for.
- **A worker's result is `Nil`,** because nothing reads it. Its invocation is still logged as succeeding or failing. Anything more, such as how many Things it restored, goes in a log line the worker writes itself.
- **A feature that won't fit a family is usually two features.** A command that also returns a lot of information can return a link to a query instead.

### Naming

The name says the family, so that a reader of the feature list can tell what each one does without opening it.

- **Queries are named after the information they return,** as noun phrases: `Things`, `ApplicationsAwaitingExamination`. Invoking one is like asking for the information.
- **Commands are named after the change they make,** as verb phrases: `DeleteThing`, `SubmitApplication`. Invoking one is like giving an instruction.
- **Workers are named `Handle` followed by what triggers them:** `HandleRestoreDue`, `HandleIdentityDetailsChanged`.

A feature's name is part of every link to it, so renaming one is a contract change (see "Contracts").

### Result cases

When a feature's outcomes differ in kind, its result is an abstract record with a nested record for each *result case*, in words the stakeholders would recognise:

```csharp
public abstract record Result
{
    private Result() { }   // only the cases below can derive from it

    public record Renamed(Thing Thing, Things.Args LinkToThings) : Result;
    public record NeedsNewName(Thing Thing, Args LinkToRename) : Result;
    public record NotFound(string Id) : Result;
}
```

- **An expected outcome is a case, not an exception.** A Thing not being there isn't exceptional; it's something that happens. Exceptions are for what nobody expected: the database is down, or the code is wrong.
- **Cases stay unsealed,** so that their JSON names the case (`"$case":"Renamed"`) however they're written.

## Links

A feature's `Args` *is* a link: a value that names the feature and carries what's needed to invoke it. Links are written in Core, as ordinary code, so the compiler checks every one. Change a feature's arguments and every place that links to it stops compiling.

- **Outputs offer links to what the caller can do next.** `Things` offers, for each Thing, a link to delete it and a link to rename it. A command's success case leads somewhere, such as `DeleteThing`'s link back to `Things`.
- **Offer only links the caller may follow.** Ask `LinkPermissions.MayInvoke` before offering a link, and offer `null` otherwise. Then the user isn't offered something that's bound to fail, and the rules about who may do what live in one place. Offering a link proves nothing later: the feature's rule checks it again when it comes back.
- **A link may leave an argument to be supplied.** `Things` offers `RenameThing.Args(thing.Id, null)`. Invoked like that, `RenameThing` returns `NeedsNewName`, so a UI asks for the name and follows the link again. Make the argument nullable, and return a case that says what's missing and hands the link back, as filled in so far, to complete. Then the caller needn't keep the link it came with, and a generic UI can offer it like any other.
- **When an obstacle can be got round, the case that reports it offers the way round,** as a link.
- **Never build a URL in Core.** Core doesn't know how it's exposed. Entrypoints translate links into URLs where they need them.

## Features don't invoke features

A feature that leads to another returns a link to it, and whoever holds the link decides whether to follow it. A feature never invokes another, and can't: features can't be injected into anything, and Core can't see `Features`.

This keeps each feature a whole job of its own, with its own rule, logs and trace. Logic two features share goes in a facility both use (see "Core").

## Who may invoke

Every feature is registered, in each entrypoint that invokes it, with between one and three rules. A rule is an authorization handler for `InvokeRequirement`, with the feature's `Args` as the resource, so it sees the caller and the arguments. Only what a rule approves is invoked. There's no other way in: a feature with no rule can't be invoked, and `[AllowAnonymous]` isn't honoured.

- **A rule that belongs to one feature is nested in it,** as `DeleteThing.Rule` is.
- **Otherwise use a ready-made rule:** `AnyCaller`, `AnySignedInCaller` or `AnySystemCaller`.
- **Workers are registered with `AnySystemCaller`,** so only the service's own jobs and queues invoke them. A job invokes as `SystemCaller.For(<the job's name>)`.

## Core

Core holds the features and everything that supports them. It references only `Featurewise.Core`, and knows nothing about the entrypoints.

- `Models/`: the domain's types, such as `Thing`.
- `Features/`: the features.
- `Facilities/`: everything else the features use, such as `IThingsPersistence` and `IChanges`. A facility is anything shared: a store, a client for another system, logic two features both need.
- `Service.cs`: the service's id, which qualifies every link's feature name, as in `sample/DeleteThing`. It's in every link handed out, so it never changes.

A facility's implementation lives with its interface, in Core, unless the service runs in more than one place. Most services run in one place, and a project boundary there guards against a migration that never comes. This sample is the exception: it runs on AWS and on a laptop, so its implementations are in `SampleService.Aws` and `SampleService.Local`.

## Entrypoints

An *entrypoint* is a project for one channel through which features are invoked: a UI backend, a public API, a scheduler, a queue. It has one job, translation. It turns whatever arrived into a link, invokes it through `Features`, and turns the result into whatever its channel expects. If an entrypoint starts having opinions about Things, something has gone wrong: anything the service does belongs in a feature.

- **Each entrypoint registers only the features it invokes,** each with the rules that apply through it. A feature can be open to more callers through one entrypoint than another.
- **The UI backend's contract is the features themselves:** a query is `GET /{feature}?link=`, and a command is `POST /{feature}` with the link as the body. A link names its feature in a property that says its family (`$query`, `$command` or `$worker`), so the UI knows which to use before it follows one. Results go out with their links and cases. A GET for a command is refused, so a page reload never runs one again.
- **A public API has a contract of its own,** described in its OpenAPI document: versioned routes, its own shapes, no links or cases. Its GET routes invoke only queries.
- **A scheduled job is a link.** The schedule carries the worker's link, and one function runs every schedule.
- **An event is a link too.** The queue entrypoint turns each delivered event into its worker's link, and invokes it.

## Changes and events

A feature's changes happen together or not at all, and so do the events it raises. Nothing is left half done.

- **Facilities stage writes, and the feature commits them once.** A facility that changes state doesn't save straight away: it stages its writes in the invocation's unit of work, `IChanges`, and the feature calls `Commit` when it has decided. So each facility stays independent. None takes a transaction as an argument, and none knows about the others.
- **Commit before deciding the result.** The result says what happened, so it can't be chosen until the commit has succeeded. Committing after the feature returns would mean answering first and finding out later.
- **What must still be true when the writes land is a condition on the write, not a read.** Reads aren't part of the commit, and on some stores, such as DynamoDB, they can't be. `DeleteThing` stages "delete it, only if it's still there", so the commit fails if another invocation deleted it first, and the feature turns that failure into a result case, `NotFound`.
- **An invocation that stages writes and never commits is a bug.** The in-memory unit of work the tests use throws when that happens.
- **Events are raised through a facility, into the same commit.** Raising one stages a record beside the writes, in an *outbox*, and a relay delivers committed records to the workers. So an event goes out if, and only if, the change happened.
- **Delivery is at least once,** so a worker an event triggers is idempotent (see "Idempotency").
- **The commit carries the invocation's idempotency key** when the store can use one, so a retried request commits once.

The sample raises no events yet, so the rules about them have no example here.

## Idempotency

Queues deliver messages at least once, and clients retry. So any feature may be invoked twice for one request.

- **Queries needn't care.**
- **A command or worker that mustn't act twice is idempotent,** either through its own data, as deleting a Thing that's already gone returns `NotFound`, or through the request's `Invocation.IdempotencyKey`. What a repeat means belongs to the feature.
- **Entrypoints pass on an idempotency key when the channel has one:** an `Idempotency-Key` header, a message id, a schedule's execution id.

## Contracts

Links outlive the code that made them: a browser tab holds one across a deployment, and a queue holds one through it. The same goes for a public API and for any events the service publishes. Change them compatibly, since environments don't update all at once.

- **Adding a nullable or defaulted argument is safe.**
- **Removing, renaming or requiring an argument takes two releases:** first accept both, then stop accepting the old.
- **A feature's name, a case's name and the service's id are in every link and result.** Renaming one is the same kind of change.

## Tests

- **Test a feature by invoking it,** through a standalone `Features` over in-memory facilities, as a caller who may or may not invoke it. No HTTP is needed.
- **Follow links in tests,** as a user would: invoke a query, take a link from its result, and invoke that.
- **Test each entrypoint with what its channel delivers,** such as the event API Gateway sends, so that the translation is tested too.
- **For a behaviour change, check that its test fails without the new code.**
