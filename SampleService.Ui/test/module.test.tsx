import { act } from "react";
import { beforeEach, describe, expect, it } from "vitest";
import sample from "../src/index";
import type { Host, Link, ShellResponse } from "@ada74m/featurewise-ui";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const response = (feature: string, body: unknown, link: Link = { $command: feature }): ShellResponse =>
  ({ link, feature, family: link.$query ? "query" : "command", status: 200, contentType: "application/json", body });

describe("the sample's module", () => {
  let followed: Link[];
  let toasts: string[];
  let host: Host;
  let element: HTMLElement;

  beforeEach(() => {
    followed = [];
    toasts = [];
    host = { follow: link => followed.push(link), toast: message => toasts.push(message), embed: () => () => {}, pathOf: () => null };
    element = document.createElement("div");
    document.body.replaceChildren(element);
  });

  it("gives Things an address, and nothing else", () => {
    expect(sample.addressToLink("things")).toEqual({ $query: "sample/Things" });
    expect(sample.addressToLink("")).toEqual({ $query: "sample/Things" });
    expect(sample.addressToLink("elsewhere")).toBeNull();
    expect(sample.linkToAddress({ $query: "sample/Things" })).toBe("things");
    expect(sample.linkToAddress({ $command: "sample/DeleteThing", Id: "t1" })).toBeNull();
  });

  it("leaves Things to the generic renderer", () => {
    expect(sample.render(element, response("sample/Things", { Things: [] }, { $query: "sample/Things" }), host)).toBeNull();
  });

  it("goes back to the list after a delete, saying so", () => {
    sample.render(element, response("sample/DeleteThing", { $case: "Deleted", LinkToThings: { $query: "sample/Things" } }), host);

    expect(toasts).toEqual(["Deleted."]);
    expect(followed).toEqual([{ $query: "sample/Things" }]);
  });

  it("asks for a new name, and follows the link it was handed back with it", async () => {
    const link = { $command: "sample/RenameThing", Id: "t1", NewName: null };
    const body = { $case: "NeedsNewName", Thing: { Id: "t1", Name: "Teapot" }, LinkToRename: link };
    let clear: (() => void) | null = null;
    await act(async () => {
      clear = sample.render(element, response("sample/RenameThing", body, { $command: "sample/RenameThing", Id: "elsewhere", NewName: null }), host);
    });

    const input = element.querySelector("input")!;
    expect(input.value).toBe("Teapot");
    await act(async () => {
      const setValue = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
      setValue.call(input, "Kettle");
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
    await act(async () => {
      element.querySelector("form")!.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
    });

    expect(followed).toEqual([{ $command: "sample/RenameThing", Id: "t1", NewName: "Kettle" }]);
    act(() => clear!());
  });
});
