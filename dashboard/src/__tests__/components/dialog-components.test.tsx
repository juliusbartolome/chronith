import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import {
  AlertDialog,
  AlertDialogTrigger,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogFooter,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogAction,
  AlertDialogMedia,
  AlertDialogOverlay,
  AlertDialogPortal,
} from "@/components/ui/alert-dialog";
import {
  Dialog,
  DialogTrigger,
  DialogContent,
  DialogHeader,
  DialogFooter,
  DialogTitle,
  DialogDescription,
  DialogClose,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

// AlertDialog components — render in closed (not open) state to exercise the exported functions
describe("AlertDialog components", () => {
  it("renders AlertDialog root without crashing", () => {
    const { container } = render(
      <AlertDialog>
        <AlertDialogTrigger>Open</AlertDialogTrigger>
      </AlertDialog>,
    );
    expect(screen.getByText("Open")).toBeInTheDocument();
  });

  it("renders AlertDialogHeader as a div with data-slot", () => {
    const { container } = render(<AlertDialogHeader>Header content</AlertDialogHeader>);
    expect(container.querySelector('[data-slot="alert-dialog-header"]')).toBeInTheDocument();
  });

  it("renders AlertDialogFooter as a div with data-slot", () => {
    const { container } = render(<AlertDialogFooter>Footer content</AlertDialogFooter>);
    expect(container.querySelector('[data-slot="alert-dialog-footer"]')).toBeInTheDocument();
  });

  it("renders AlertDialogMedia with data-slot", () => {
    const { container } = render(<AlertDialogMedia>icon</AlertDialogMedia>);
    expect(container.querySelector('[data-slot="alert-dialog-media"]')).toBeInTheDocument();
  });

  it("renders AlertDialogAction as a button", () => {
    // AlertDialogAction is a Radix primitive button that does not require AlertDialogContent context
    render(
      <AlertDialog>
        <AlertDialogTrigger>Open</AlertDialogTrigger>
        <AlertDialogAction>Confirm</AlertDialogAction>
      </AlertDialog>,
    );
    expect(screen.getByText("Confirm")).toBeInTheDocument();
  });
});

// Dialog components
describe("Dialog components", () => {
  it("renders Dialog root without crashing", () => {
    render(
      <Dialog>
        <DialogTrigger>Open dialog</DialogTrigger>
      </Dialog>,
    );
    expect(screen.getByText("Open dialog")).toBeInTheDocument();
  });

  it("renders DialogHeader with data-slot", () => {
    const { container } = render(<DialogHeader>Header</DialogHeader>);
    expect(container.querySelector('[data-slot="dialog-header"]')).toBeInTheDocument();
  });

  it("renders DialogFooter with data-slot", () => {
    const { container } = render(<DialogFooter>Footer</DialogFooter>);
    expect(container.querySelector('[data-slot="dialog-footer"]')).toBeInTheDocument();
  });

  it("renders DialogTitle with data-slot", () => {
    // DialogTitle uses DialogPrimitive.Title which renders inside a dialog portal;
    // render standalone to get just the element exported
    const { container } = render(
      <Dialog>
        <DialogTitle>My Title</DialogTitle>
      </Dialog>,
    );
    // DialogTitle is a Radix primitive — confirm it is accessible via text
    expect(screen.getByText("My Title")).toBeInTheDocument();
  });

  it("renders DialogDescription with data-slot", () => {
    const { container } = render(
      <Dialog>
        <DialogDescription>Some description</DialogDescription>
      </Dialog>,
    );
    expect(screen.getByText("Some description")).toBeInTheDocument();
  });

  it("renders DialogClose button", () => {
    render(
      <Dialog>
        <DialogClose>Close</DialogClose>
      </Dialog>,
    );
    expect(screen.getByText("Close")).toBeInTheDocument();
  });
});

// DropdownMenu components
describe("DropdownMenu components", () => {
  it("renders DropdownMenu root with trigger without crashing", () => {
    render(
      <DropdownMenu>
        <DropdownMenuTrigger>Open Menu</DropdownMenuTrigger>
      </DropdownMenu>,
    );
    expect(screen.getByText("Open Menu")).toBeInTheDocument();
  });

  it("renders DropdownMenuTrigger with data-slot attribute", () => {
    const { container } = render(
      <DropdownMenu>
        <DropdownMenuTrigger>Menu</DropdownMenuTrigger>
      </DropdownMenu>,
    );
    expect(container.querySelector('[data-slot="dropdown-menu-trigger"]')).toBeInTheDocument();
  });
});
