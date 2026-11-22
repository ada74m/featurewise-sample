import type { Host, Link } from "@ada74m/featurewise-ui";

// A command's success case that leads back to a query: say what happened, and go there.
export function toastAndFollow(host: Host, message: string, next: Link): () => void {
  host.toast(message);
  host.follow(next);
  return () => {};
}
