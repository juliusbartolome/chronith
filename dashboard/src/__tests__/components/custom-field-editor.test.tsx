import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { CustomFieldEditor } from "@/components/booking-types/custom-field-editor";

describe("CustomFieldEditor", () => {
  it("shows empty state when no fields", () => {
    render(<CustomFieldEditor fields={[]} onChange={vi.fn()} />);
    expect(screen.getByText(/no custom fields/i)).toBeInTheDocument();
  });

  it("renders existing fields", () => {
    render(
      <CustomFieldEditor
        fields={[{ name: "Notes", type: "text", isRequired: false }]}
        onChange={vi.fn()}
      />,
    );
    expect(screen.getByText("Notes")).toBeInTheDocument();
  });

  it("adds a new field when Add Field is clicked with a name", () => {
    const onChange = vi.fn();
    render(<CustomFieldEditor fields={[]} onChange={onChange} />);
    fireEvent.change(screen.getByLabelText(/field name/i), {
      target: { value: "Color" },
    });
    fireEvent.click(screen.getByRole("button", { name: /add field/i }));
    expect(onChange).toHaveBeenCalledWith([
      { name: "Color", type: "text", isRequired: false },
    ]);
  });

  it("does not add field when name is empty", () => {
    const onChange = vi.fn();
    render(<CustomFieldEditor fields={[]} onChange={onChange} />);
    fireEvent.click(screen.getByRole("button", { name: /add field/i }));
    expect(onChange).not.toHaveBeenCalled();
  });

  it("removes a field on × click", () => {
    const onChange = vi.fn();
    render(
      <CustomFieldEditor
        fields={[{ name: "Notes", type: "text", isRequired: false }]}
        onChange={onChange}
      />,
    );
    fireEvent.click(screen.getByLabelText("Remove field Notes"));
    expect(onChange).toHaveBeenCalledWith([]);
  });
});
