import type { Renderer } from "@ada74m/featurewise-ui";
import { toastAndFollow } from "../kit";

// DeleteThing: Deleted says so, and goes back to the list. NotFound is left to the generic renderer.

export const Deleted: Renderer = (_, body, host) => toastAndFollow(host, "Deleted.", body.LinkToThings);
