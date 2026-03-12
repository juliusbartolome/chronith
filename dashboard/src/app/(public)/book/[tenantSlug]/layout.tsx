import CustomerAuthHeader from "@/components/public/customer-auth-header";

// Server component — fetch tenant settings for branding
async function getTenantBranding(tenantSlug: string) {
  const apiUrl = process.env.CHRONITH_API_URL ?? "http://localhost:5001";
  try {
    const res = await fetch(
      `${apiUrl}/v1/public/${tenantSlug}/settings`,
      { cache: "no-store" },
    );
    if (!res.ok) return null;
    return res.json() as Promise<{
      logoUrl: string | null;
      primaryColor: string;
      accentColor: string | null;
      welcomeMessage: string | null;
    }>;
  } catch {
    return null;
  }
}

export default async function PublicBookingLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ tenantSlug: string }>;
}) {
  const { tenantSlug } = await params;
  const branding = await getTenantBranding(tenantSlug);

  const primaryColor = branding?.primaryColor ?? "#2563EB";
  const accentColor = branding?.accentColor ?? "#6366F1";

  return (
    <div
      style={
        {
          "--color-primary": primaryColor,
          "--color-accent": accentColor,
        } as React.CSSProperties
      }
    >
      {/* Minimal public header */}
      <header className="border-b bg-white px-4 py-3 flex items-center gap-3">
        {branding?.logoUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={branding.logoUrl}
            alt="Logo"
            className="h-8 object-contain"
          />
        ) : (
          <span className="font-semibold text-zinc-800">Chronith</span>
        )}
        <div className="ml-auto">
          <CustomerAuthHeader tenantSlug={tenantSlug} />
        </div>
      </header>

      <main className="min-h-screen bg-zinc-50">
        {children}
      </main>
    </div>
  );
}
