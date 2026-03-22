"use client";

import { useState, FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { CheckCircle, XCircle, Loader2, Send } from "lucide-react";

interface FormState {
  name: string;
  email: string;
  subject: string;
  message: string;
}

export function ContactForm() {
  const [formData, setFormData] = useState<FormState>({
    name: "",
    email: "",
    subject: "",
    message: "",
  });
  const [status, setStatus] = useState<"idle" | "submitting" | "success" | "error">("idle");
  const [errors, setErrors] = useState<Partial<FormState>>({});

  function validateForm(): boolean {
    const newErrors: Partial<FormState> = {};

    if (!formData.name.trim()) {
      newErrors.name = "Name is required";
    }

    if (!formData.email.trim()) {
      newErrors.email = "Email is required";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
      newErrors.email = "Please enter a valid email address";
    }

    if (!formData.subject.trim()) {
      newErrors.subject = "Subject is required";
    }

    if (!formData.message.trim()) {
      newErrors.message = "Message is required";
    } else if (formData.message.trim().length < 10) {
      newErrors.message = "Message must be at least 10 characters";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();

    if (!validateForm()) return;

    setStatus("submitting");

    // Simulate form submission (no backend needed yet)
    console.log("Contact form submitted:", formData);

    // Simulate network delay
    await new Promise((resolve) => setTimeout(resolve, 1500));

    // For now, always succeed (backend integration not implemented)
    setStatus("success");
  }

  function handleChange(
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
  ) {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    // Clear error when user starts typing
    if (errors[name as keyof FormState]) {
      setErrors((prev) => ({ ...prev, [name]: undefined }));
    }
  }

  if (status === "success") {
    return (
      <Card>
        <CardContent className="pt-6">
          <div className="flex flex-col items-center text-center py-8">
            <CheckCircle className="h-12 w-12 text-green-500 mb-4" />
            <h3 className="text-lg font-semibold text-zinc-900 mb-2">
              Message Sent Successfully!
            </h3>
            <p className="text-zinc-600 mb-4">
              Thank you for reaching out. We&apos;ll get back to you within 24-48 hours.
            </p>
            <Button
              variant="outline"
              onClick={() => {
                setStatus("idle");
                setFormData({ name: "", email: "", subject: "", message: "" });
              }}
            >
              Send Another Message
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-xl">Send us a message</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid gap-2">
            <Label htmlFor="name">Name</Label>
            <Input
              id="name"
              name="name"
              type="text"
              placeholder="Your name"
              value={formData.name}
              onChange={handleChange}
              disabled={status === "submitting"}
              aria-invalid={!!errors.name}
            />
            {errors.name && (
              <p className="text-sm text-red-500 flex items-center gap-1">
                <XCircle className="h-3 w-3" />
                {errors.name}
              </p>
            )}
          </div>

          <div className="grid gap-2">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              name="email"
              type="email"
              placeholder="you@example.com"
              value={formData.email}
              onChange={handleChange}
              disabled={status === "submitting"}
              aria-invalid={!!errors.email}
            />
            {errors.email && (
              <p className="text-sm text-red-500 flex items-center gap-1">
                <XCircle className="h-3 w-3" />
                {errors.email}
              </p>
            )}
          </div>

          <div className="grid gap-2">
            <Label htmlFor="subject">Subject</Label>
            <Input
              id="subject"
              name="subject"
              type="text"
              placeholder="How can we help?"
              value={formData.subject}
              onChange={handleChange}
              disabled={status === "submitting"}
              aria-invalid={!!errors.subject}
            />
            {errors.subject && (
              <p className="text-sm text-red-500 flex items-center gap-1">
                <XCircle className="h-3 w-3" />
                {errors.subject}
              </p>
            )}
          </div>

          <div className="grid gap-2">
            <Label htmlFor="message">Message</Label>
            <Textarea
              id="message"
              name="message"
              placeholder="Tell us more about your inquiry..."
              rows={5}
              value={formData.message}
              onChange={handleChange}
              disabled={status === "submitting"}
              aria-invalid={!!errors.message}
            />
            {errors.message && (
              <p className="text-sm text-red-500 flex items-center gap-1">
                <XCircle className="h-3 w-3" />
                {errors.message}
              </p>
            )}
          </div>

          <Button
            type="submit"
            className="w-full"
            disabled={status === "submitting"}
          >
            {status === "submitting" ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                Sending...
              </>
            ) : (
              <>
                <Send className="h-4 w-4" />
                Send Message
              </>
            )}
          </Button>

          {status === "error" && (
            <p className="text-sm text-red-500 text-center flex items-center justify-center gap-1">
              <XCircle className="h-3 w-3" />
              Something went wrong. Please try again.
            </p>
          )}
        </form>
      </CardContent>
    </Card>
  );
}
