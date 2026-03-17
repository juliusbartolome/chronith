import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardAction,
  CardContent,
  CardFooter,
} from "@/components/ui/card";
import {
  Table,
  TableHeader,
  TableBody,
  TableFooter,
  TableRow,
  TableHead,
  TableCell,
  TableCaption,
} from "@/components/ui/table";
import {
  SelectGroup,
  SelectLabel,
  SelectSeparator,
} from "@/components/ui/select";

describe("Skeleton", () => {
  it("renders with default classes", () => {
    const { container } = render(<Skeleton />);
    const el = container.querySelector('[data-slot="skeleton"]');
    expect(el).toBeInTheDocument();
    expect(el).toHaveClass("animate-pulse");
  });

  it("renders with custom className", () => {
    const { container } = render(<Skeleton className="w-10 h-10" />);
    const el = container.querySelector('[data-slot="skeleton"]');
    expect(el).toHaveClass("w-10");
  });

  it("forwards additional props", () => {
    render(<Skeleton data-testid="my-skeleton" />);
    expect(screen.getByTestId("my-skeleton")).toBeInTheDocument();
  });
});

describe("Textarea", () => {
  it("renders a textarea element", () => {
    render(<Textarea placeholder="Enter text" />);
    expect(screen.getByPlaceholderText("Enter text")).toBeInTheDocument();
  });

  it("renders with custom className", () => {
    render(<Textarea className="my-custom" data-testid="ta" />);
    expect(screen.getByTestId("ta")).toHaveClass("my-custom");
  });

  it("forwards props like disabled", () => {
    render(<Textarea disabled data-testid="ta" />);
    expect(screen.getByTestId("ta")).toBeDisabled();
  });

  it("has data-slot attribute", () => {
    render(<Textarea data-testid="ta" />);
    expect(screen.getByTestId("ta")).toHaveAttribute("data-slot", "textarea");
  });
});

describe("Card components", () => {
  it("renders Card with data-slot", () => {
    const { container } = render(<Card>content</Card>);
    expect(container.querySelector('[data-slot="card"]')).toBeInTheDocument();
  });

  it("renders CardHeader", () => {
    const { container } = render(<CardHeader>header</CardHeader>);
    expect(
      container.querySelector('[data-slot="card-header"]'),
    ).toBeInTheDocument();
  });

  it("renders CardTitle", () => {
    const { container } = render(<CardTitle>Title</CardTitle>);
    expect(
      container.querySelector('[data-slot="card-title"]'),
    ).toBeInTheDocument();
  });

  it("renders CardDescription", () => {
    const { container } = render(<CardDescription>Desc</CardDescription>);
    expect(
      container.querySelector('[data-slot="card-description"]'),
    ).toBeInTheDocument();
  });

  it("renders CardAction", () => {
    const { container } = render(<CardAction>Action</CardAction>);
    expect(
      container.querySelector('[data-slot="card-action"]'),
    ).toBeInTheDocument();
  });

  it("renders CardContent", () => {
    const { container } = render(<CardContent>Content</CardContent>);
    expect(
      container.querySelector('[data-slot="card-content"]'),
    ).toBeInTheDocument();
  });

  it("renders CardFooter", () => {
    const { container } = render(<CardFooter>Footer</CardFooter>);
    expect(
      container.querySelector('[data-slot="card-footer"]'),
    ).toBeInTheDocument();
  });

  it("renders a full Card with all subcomponents", () => {
    render(
      <Card>
        <CardHeader>
          <CardTitle>Test Card</CardTitle>
          <CardDescription>A test card</CardDescription>
          <CardAction>Edit</CardAction>
        </CardHeader>
        <CardContent>Body content</CardContent>
        <CardFooter>Footer content</CardFooter>
      </Card>,
    );
    expect(screen.getByText("Test Card")).toBeInTheDocument();
    expect(screen.getByText("A test card")).toBeInTheDocument();
    expect(screen.getByText("Body content")).toBeInTheDocument();
    expect(screen.getByText("Footer content")).toBeInTheDocument();
  });
});

describe("Table components", () => {
  it("renders Table with container and table elements", () => {
    const { container } = render(
      <Table>
        <TableBody>
          <TableRow>
            <TableCell>Cell</TableCell>
          </TableRow>
        </TableBody>
      </Table>,
    );
    expect(
      container.querySelector('[data-slot="table-container"]'),
    ).toBeInTheDocument();
    expect(container.querySelector('[data-slot="table"]')).toBeInTheDocument();
  });

  it("renders TableHeader", () => {
    const { container } = render(
      <table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
          </TableRow>
        </TableHeader>
      </table>,
    );
    expect(
      container.querySelector('[data-slot="table-header"]'),
    ).toBeInTheDocument();
    expect(
      container.querySelector('[data-slot="table-head"]'),
    ).toBeInTheDocument();
  });

  it("renders TableFooter", () => {
    const { container } = render(
      <table>
        <TableFooter>
          <TableRow>
            <TableCell>Total</TableCell>
          </TableRow>
        </TableFooter>
      </table>,
    );
    expect(
      container.querySelector('[data-slot="table-footer"]'),
    ).toBeInTheDocument();
  });

  it("renders TableCaption", () => {
    const { container } = render(
      <table>
        <TableCaption>My Table</TableCaption>
        <TableBody>
          <TableRow>
            <TableCell>Row</TableCell>
          </TableRow>
        </TableBody>
      </table>,
    );
    expect(
      container.querySelector('[data-slot="table-caption"]'),
    ).toBeInTheDocument();
    expect(screen.getByText("My Table")).toBeInTheDocument();
  });

  it("renders full table", () => {
    render(
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Status</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow>
            <TableCell>Alice</TableCell>
            <TableCell>Active</TableCell>
          </TableRow>
        </TableBody>
        <TableFooter>
          <TableRow>
            <TableCell>Total: 1</TableCell>
          </TableRow>
        </TableFooter>
      </Table>,
    );
    expect(screen.getByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("Status")).toBeInTheDocument();
  });
});

describe("Select components", () => {
  it("renders SelectGroup with data-slot", () => {
    const { container } = render(
      <SelectGroup data-testid="sg">
        <div>option</div>
      </SelectGroup>,
    );
    expect(container.querySelector('[data-slot="select-group"]')).toBeTruthy();
  });

  it("renders SelectLabel with data-slot", () => {
    const { container } = render(
      <SelectGroup>
        <SelectLabel>Fruits</SelectLabel>
      </SelectGroup>,
    );
    expect(container.querySelector('[data-slot="select-label"]')).toBeTruthy();
  });

  it("renders SelectSeparator with data-slot", () => {
    const { container } = render(<SelectSeparator />);
    expect(
      container.querySelector('[data-slot="select-separator"]'),
    ).toBeTruthy();
  });
});
