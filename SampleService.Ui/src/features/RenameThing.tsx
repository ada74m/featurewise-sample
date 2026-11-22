import { useState } from "react";
import { createRoot } from "react-dom/client";
import type { Host, Link, Renderer } from "@featurewise/ui";
import { toastAndFollow } from "../kit";

// RenameThing: Renamed says so, and goes back to the list. NeedsNewName asks for the name the link
// left out, then follows the link it handed back with it. NotFound is left to the generic renderer.

export const Renamed: Renderer = (_, body, host) =>
  toastAndFollow(host, `Renamed to ${body.Thing.Name}.`, body.LinkToThings);

export const NeedsNewName: Renderer = (element, body, host) => {
  const root = createRoot(element);
  root.render(<RenameForm link={body.LinkToRename} name={body.Thing.Name} host={host} />);
  return () => root.unmount();
};

const things: Link = { $query: "sample/Things" };

function RenameForm({ link, name, host }: { link: Link; name: string; host: Host }) {
  const [newName, setNewName] = useState(name);

  return (
    <form className="fw-stack" onSubmit={event => {
      event.preventDefault();
      host.follow({ ...link, NewName: newName });
    }}>
      <h1>Rename {name}</h1>
      <label>
        New name <input value={newName} onChange={event => setNewName(event.target.value)} autoFocus />
      </label>
      <div className="fw-row">
        <button type="button" onClick={() => host.follow(things)}>Cancel</button>
        <button type="submit" className="fw-primary">Rename</button>
      </div>
    </form>
  );
}
