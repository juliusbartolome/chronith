export function formatPrice(centavos: number): string {
  if (centavos === 0) return "Free";
  return `₱${(centavos / 100).toLocaleString("en-PH", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}
