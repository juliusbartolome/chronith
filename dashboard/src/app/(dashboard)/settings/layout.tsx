import Link from "next/link";

const SETTINGS_NAV = [
  { href: "/settings/profile", label: "Profile" },
  { href: "/settings/auth", label: "Auth Config" },
  { href: "/settings/api-keys", label: "API Keys" },
];

export default function SettingsLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex gap-8">
      <nav className="w-48 shrink-0 space-y-1">
        {SETTINGS_NAV.map(({ href, label }) => (
          <Link
            key={href}
            href={href}
            className="block rounded-md px-3 py-2 text-sm font-medium text-zinc-600 hover:bg-zinc-100 hover:text-zinc-900"
          >
            {label}
          </Link>
        ))}
      </nav>
      <div className="flex-1">{children}</div>
    </div>
  );
}
