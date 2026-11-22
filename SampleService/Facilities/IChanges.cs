namespace SampleService;

// The invocation's unit of work. Facilities that change state stage their writes here instead of writing
// straight away, and the feature commits them once, when it has decided: all of them are written, or none.
public interface IChanges
{
    // Writes everything staged, together. False when a condition a facility attached to a write no longer
    // holds, such as a Thing still being there; then nothing is written.
    Task<bool> Commit(CancellationToken cancellationToken);
}
