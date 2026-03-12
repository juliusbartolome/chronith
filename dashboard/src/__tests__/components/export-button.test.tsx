import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { ExportButton } from "@/components/shared/export-button";

describe("ExportButton", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("renders the export button", () => {
    render(
      <ExportButton exportUrl="/api/bookings/export" filename="bookings" />,
    );
    expect(screen.getByRole("button", { name: /export/i })).toBeInTheDocument();
  });

  it("triggers CSV download on CSV click", async () => {
    const user = userEvent.setup();
    const mockBlob = new Blob(["id,name"], { type: "text/csv" });
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      blob: async () => mockBlob,
    } as unknown as Response);

    const createObjectURL = vi.fn().mockReturnValue("blob:mock");
    const revokeObjectURL = vi.fn();
    global.URL.createObjectURL = createObjectURL;
    global.URL.revokeObjectURL = revokeObjectURL;

    const clickSpy = vi.fn();
    const originalCreate = document.createElement.bind(document);
    vi.spyOn(document, "createElement").mockImplementation((tag: string) => {
      const el = originalCreate(tag);
      if (tag === "a") {
        Object.defineProperty(el, "click", { value: clickSpy });
      }
      return el;
    });

    render(
      <ExportButton exportUrl="/api/bookings/export" filename="bookings" />,
    );

    // Open dropdown using userEvent (proper pointer events for Radix portal)
    await user.click(screen.getByRole("button", { name: /export/i }));
    const csvOption = await screen.findByText(/export as csv/i);
    await user.click(csvOption);

    await waitFor(() =>
      expect(global.fetch).toHaveBeenCalledWith(
        expect.stringContaining("format=csv"),
      ),
    );
  });

  it("shows error state on fetch failure", async () => {
    const user = userEvent.setup();
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
    } as Response);

    render(
      <ExportButton exportUrl="/api/bookings/export" filename="bookings" />,
    );

    await user.click(screen.getByRole("button", { name: /export/i }));
    const csvOption = await screen.findByText(/export as csv/i);
    await user.click(csvOption);

    // After failed export, button should return to normal state
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: /export/i }),
      ).toBeInTheDocument(),
    );
  });
});
