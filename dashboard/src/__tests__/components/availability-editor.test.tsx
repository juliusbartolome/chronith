import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { AvailabilityEditor } from "@/components/staff/availability-editor";

describe("AvailabilityEditor", () => {
  it("renders unavailable for all days when windows is empty", () => {
    render(<AvailabilityEditor windows={[]} onChange={vi.fn()} />);
    const unavailable = screen.getAllByText("Unavailable");
    expect(unavailable).toHaveLength(7);
  });

  it("renders existing windows", () => {
    render(
      <AvailabilityEditor
        windows={[{ dayOfWeek: 1, startTime: "09:00", endTime: "17:00" }]}
        onChange={vi.fn()}
      />,
    );
    expect(screen.getByText("09:00 – 17:00")).toBeInTheDocument();
  });

  it("calls onChange with new window when Add Window is clicked", () => {
    const onChange = vi.fn();
    render(<AvailabilityEditor windows={[]} onChange={onChange} />);
    fireEvent.click(screen.getByRole("button", { name: /add window/i }));
    expect(onChange).toHaveBeenCalledWith([
      { dayOfWeek: 1, startTime: "09:00", endTime: "17:00" },
    ]);
  });

  it("calls onChange removing a window when × is clicked", () => {
    const onChange = vi.fn();
    render(
      <AvailabilityEditor
        windows={[{ dayOfWeek: 1, startTime: "09:00", endTime: "17:00" }]}
        onChange={onChange}
      />,
    );
    fireEvent.click(screen.getByLabelText("Remove window"));
    expect(onChange).toHaveBeenCalledWith([]);
  });
});
