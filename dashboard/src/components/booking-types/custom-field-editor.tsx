"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

export interface CustomField {
  name: string;
  type: "text" | "number" | "boolean" | "date";
  isRequired: boolean;
}

interface Props {
  fields: CustomField[];
  onChange: (fields: CustomField[]) => void;
}

export function CustomFieldEditor({ fields, onChange }: Props) {
  const [name, setName] = useState("");
  const [type, setType] = useState<CustomField["type"]>("text");
  const [isRequired, setIsRequired] = useState(false);

  const addField = () => {
    if (!name.trim()) return;
    onChange([...fields, { name: name.trim(), type, isRequired }]);
    setName("");
    setType("text");
    setIsRequired(false);
  };

  const removeField = (index: number) => {
    onChange(fields.filter((_, i) => i !== index));
  };

  return (
    <div className="space-y-4">
      {fields.length === 0 && (
        <p className="text-sm text-zinc-400">No custom fields defined.</p>
      )}

      <div className="space-y-2">
        {fields.map((f, i) => (
          <div
            key={i}
            className="flex items-center gap-3 rounded-md border px-3 py-2 text-sm"
          >
            <span className="flex-1 font-medium">{f.name}</span>
            <span className="rounded bg-zinc-100 px-2 py-0.5 text-xs text-zinc-600">
              {f.type}
            </span>
            {f.isRequired && (
              <span className="text-xs text-red-600">required</span>
            )}
            <Button
              variant="ghost"
              size="sm"
              className="h-6 w-6 p-0 text-zinc-400 hover:text-red-600"
              onClick={() => removeField(i)}
              aria-label={`Remove field ${f.name}`}
            >
              ×
            </Button>
          </div>
        ))}
      </div>

      <div className="flex items-end gap-3 border-t pt-4">
        <div className="flex-1">
          <Label htmlFor="field-name">Field Name</Label>
          <Input
            id="field-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Notes"
            className="mt-1"
          />
        </div>
        <div>
          <Label htmlFor="field-type">Type</Label>
          <Select
            value={type}
            onValueChange={(v) => setType(v as CustomField["type"])}
          >
            <SelectTrigger id="field-type" className="mt-1 w-28">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="text">Text</SelectItem>
              <SelectItem value="number">Number</SelectItem>
              <SelectItem value="boolean">Boolean</SelectItem>
              <SelectItem value="date">Date</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="flex items-center gap-2 pb-1">
          <input
            id="field-required"
            type="checkbox"
            checked={isRequired}
            onChange={(e) => setIsRequired(e.target.checked)}
            className="h-4 w-4"
          />
          <Label htmlFor="field-required" className="text-sm">
            Required
          </Label>
        </div>
        <Button type="button" variant="outline" onClick={addField}>
          Add Field
        </Button>
      </div>
    </div>
  );
}
