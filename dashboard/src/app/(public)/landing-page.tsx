import Link from "next/link";
import {
  CalendarCheck,
  Clock,
  ShieldCheck,
  Mail,
  Phone,
  MapPin,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ContactForm } from "./contact-form";

const DEFAULT_TENANT_SLUG =
  process.env.NEXT_PUBLIC_DEFAULT_TENANT_SLUG || "nexoflow-automations";

const features = [
  {
    icon: CalendarCheck,
    title: "Easy Online Booking",
    description:
      "Let customers book appointments 24/7 through your personalized booking page. No phone calls needed.",
  },
  {
    icon: Clock,
    title: "24/7 Availability",
    description:
      "Set your working hours and let the system handle scheduling. Customers can book slots that work for them.",
  },
  {
    icon: ShieldCheck,
    title: "Instant Confirmation",
    description:
      "Automatic email and SMS confirmations keep everyone informed. Reduce no-shows with reminders.",
  },
];

const contactInfo = [
  {
    icon: Mail,
    label: "Email",
    value: "hello@nexoflow.demo",
    href: "mailto:hello@nexoflow.demo",
  },
  {
    icon: Phone,
    label: "Phone",
    value: "+63 900 000 0000",
    href: "tel:+639000000000",
  },
  {
    icon: MapPin,
    label: "Location",
    value: "Manila, Philippines",
    href: null,
  },
];

export default function LandingPage() {
  return (
    <div className="flex flex-col min-h-screen bg-zinc-50">
      {/* Header */}
      <header className="border-b bg-white px-4 py-4">
        <div className="mx-auto max-w-6xl flex items-center justify-between">
          <span className="text-lg font-semibold text-zinc-900">Chronith</span>
          <span className="text-sm text-zinc-500">Powered by Chronith</span>
        </div>
      </header>
      {/* Hero Section */}
      <section className="bg-gradient-to-b from-zinc-100 to-white px-4 py-20 sm:py-32">
        <div className="mx-auto max-w-4xl text-center">
          <h1 className="text-4xl font-bold tracking-tight text-zinc-900 sm:text-5xl md:text-6xl">
            Book Appointments
            <br />
            <span className="text-blue-600">Made Simple</span>
          </h1>
          <p className="mt-6 text-lg text-zinc-600 max-w-2xl mx-auto">
            Streamline your scheduling with Chronith. Create your booking page
            in minutes and let customers book appointments on their own time.
          </p>
          <div className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4">
            <Button size="lg" asChild>
              <Link href={`/book/${DEFAULT_TENANT_SLUG}`}>
                Book Now
              </Link>
            </Button>
            <Button size="lg" variant="outline" asChild>
              <Link href="#features">Learn More</Link>
            </Button>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section id="features" className="px-4 py-16 sm:py-24 bg-white">
        <div className="mx-auto max-w-6xl">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-bold tracking-tight text-zinc-900">
              Why Choose Chronith?
            </h2>
            <p className="mt-4 text-zinc-600 max-w-xl mx-auto">
              Everything you need to manage appointments efficiently, all in one
              place.
            </p>
          </div>

          <div className="grid gap-8 md:grid-cols-3">
            {features.map((feature) => (
              <Card key={feature.title} className="border-0 shadow-md">
                <CardHeader className="pb-2">
                  <div className="h-12 w-12 rounded-lg bg-blue-100 flex items-center justify-center mb-4">
                    <feature.icon className="h-6 w-6 text-blue-600" />
                  </div>
                  <CardTitle className="text-xl">{feature.title}</CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-zinc-600">{feature.description}</p>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      </section>

      {/* Contact Section */}
      <section id="contact" className="px-4 py-16 sm:py-24 bg-zinc-50">
        <div className="mx-auto max-w-6xl">
          <div className="text-center mb-12">
            <h2 className="text-3xl font-bold tracking-tight text-zinc-900">
              Get in Touch
            </h2>
            <p className="mt-4 text-zinc-600 max-w-xl mx-auto">
              Have questions? We&apos;d love to hear from you. Send us a message
              and we&apos;ll respond as soon as possible.
            </p>
          </div>

          <div className="grid gap-12 lg:grid-cols-2">
            {/* Contact Info */}
            <div className="space-y-8">
              <div>
                <h3 className="text-lg font-semibold text-zinc-900 mb-4">
                  Contact Information
                </h3>
                <div className="space-y-4">
                  {contactInfo.map((item) => (
                    <div
                      key={item.label}
                      className="flex items-center gap-4"
                    >
                      <div className="h-10 w-10 rounded-lg bg-zinc-100 flex items-center justify-center">
                        <item.icon className="h-5 w-5 text-zinc-600" />
                      </div>
                      <div>
                        <p className="text-sm text-zinc-500">{item.label}</p>
                        {item.href ? (
                          <a
                            href={item.href}
                            className="text-zinc-900 hover:text-blue-600 transition-colors"
                          >
                            {item.value}
                          </a>
                        ) : (
                          <p className="text-zinc-900">{item.value}</p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div className="p-6 bg-blue-50 rounded-xl">
                <h3 className="text-lg font-semibold text-zinc-900 mb-2">
                  Ready to get started?
                </h3>
                <p className="text-zinc-600 mb-4">
                  Create your free booking page today and start accepting
                  appointments online.
                </p>
                <Button asChild>
                  <Link href={`/book/${DEFAULT_TENANT_SLUG}`}>
                    Book an Appointment
                  </Link>
                </Button>
              </div>
            </div>

            {/* Contact Form */}
            <div>
              <ContactForm />
            </div>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t bg-white px-4 py-6 mt-auto">
        <div className="mx-auto max-w-6xl flex flex-col sm:flex-row items-center justify-between gap-4 text-sm text-zinc-500">
          <p>© {new Date().getFullYear()} Chronith. All rights reserved.</p>
          <p>Powered by Chronith</p>
        </div>
      </footer>
    </div>
  );
}
