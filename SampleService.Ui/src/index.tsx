import { featurewiseModule } from "@ada74m/featurewise-ui";
import * as deleteThing from "./features/DeleteThing";
import * as renameThing from "./features/RenameThing";

// The sample service's UI module, a feature at a time: each query's address, and each feature's
// renderers, named after the result cases they render. Whatever isn't here is left to the shell:
// Things' result to its generic renderer, and a query with no address to its default one.
export const ui = featurewiseModule("sample", { home: "things" });

ui.addQuery("Things").address("things");

ui.addCommand("DeleteThing").renderCase("Deleted", deleteThing.Deleted);

ui.addCommand("RenameThing")
  .renderCase("Renamed", renameThing.Renamed)
  .renderCase("NeedsNewName", renameThing.NeedsNewName);

export default ui;
