'use client'

import { useState } from 'react'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Switch } from '@/components/ui/switch'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Input } from '@/components/ui/input'
import {
  useNotificationConfigs,
  useUpdateNotificationConfig,
  useDisableNotificationChannel,
  type NotificationConfigDto,
} from '@/hooks/use-notifications'
import {
  useNotificationTemplates,
  useUpdateNotificationTemplate,
  useResetNotificationTemplate,
  usePreviewNotificationTemplate,
  type NotificationTemplateDto,
} from '@/hooks/use-notification-templates'

const CHANNEL_LABELS: Record<string, string> = {
  Email: 'Email',
  Sms: 'SMS',
  Push: 'Push',
}

function ChannelCard({ config }: { config: NotificationConfigDto }) {
  const [configOpen, setConfigOpen] = useState(false)
  const update = useUpdateNotificationConfig()
  const disable = useDisableNotificationChannel()

  const handleToggle = async (enabled: boolean) => {
    if (!enabled) {
      await disable.mutateAsync(config.channelType)
    } else {
      await update.mutateAsync({
        channel: config.channelType,
        data: { channelType: config.channelType, settings: config.settings },
      })
    }
  }

  return (
    <div className="rounded-lg border p-6">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="font-semibold">{CHANNEL_LABELS[config.channelType]}</h3>
          <Badge
            variant={config.isEnabled ? 'default' : 'secondary'}
            className="mt-1"
          >
            {config.isEnabled ? 'Configured' : 'Not configured'}
          </Badge>
        </div>
        <div className="flex items-center gap-3">
          <Switch
            checked={config.isEnabled}
            onCheckedChange={handleToggle}
          />
          <Button variant="outline" size="sm" onClick={() => setConfigOpen(true)}>
            Configure
          </Button>
        </div>
      </div>

      <Dialog open={configOpen} onOpenChange={setConfigOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              Configure {CHANNEL_LABELS[config.channelType]}
            </DialogTitle>
          </DialogHeader>
          <ChannelConfigForm
            config={config}
            onSave={async (data) => {
              await update.mutateAsync({ channel: config.channelType, data })
              setConfigOpen(false)
            }}
          />
        </DialogContent>
      </Dialog>
    </div>
  )
}

function ChannelConfigForm({
  config,
  onSave,
}: {
  config: NotificationConfigDto
  onSave: (data: { channelType: string; settings: string }) => Promise<void>
}) {
  const [settings, setSettings] = useState(config.settings)

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <Label>Settings (JSON)</Label>
        <Textarea
          rows={6}
          value={settings}
          onChange={(e) => setSettings(e.target.value)}
        />
        <p className="text-xs text-muted-foreground">
          Enter settings as a JSON string.
        </p>
      </div>
      <div className="flex justify-end gap-2">
        <Button onClick={() => onSave({ channelType: config.channelType, settings })}>Save</Button>
      </div>
    </div>
  )
}

function TemplateEditor({ template }: { template: NotificationTemplateDto }) {
  const [editOpen, setEditOpen] = useState(false)
  const [subject, setSubject] = useState(template.subject ?? '')
  const [body, setBody] = useState(template.body)
  const [preview, setPreview] = useState<string | null>(null)
  const update = useUpdateNotificationTemplate()
  const reset = useResetNotificationTemplate()
  const doPreview = usePreviewNotificationTemplate()

  const handleSave = async () => {
    await update.mutateAsync({
      id: template.id,
      data: { subject, body, isActive: template.isActive },
    })
    setEditOpen(false)
  }

  const handlePreview = async () => {
    const result = await doPreview.mutateAsync({
      id: template.id,
      sampleData: {},
    })
    setPreview(result.body)
  }

  const handleReset = async () => {
    await reset.mutateAsync(template.eventType)
    setEditOpen(false)
  }

  return (
    <div className="flex items-center justify-between rounded-lg border p-4">
      <div>
        <p className="font-medium">{template.eventType}</p>
        <Badge variant="outline">{template.channelType}</Badge>
      </div>
      <Button variant="outline" size="sm" onClick={() => setEditOpen(true)}>
        Edit
      </Button>

      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {template.eventType} — {template.channelType}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            {template.subject !== undefined && (
              <div className="space-y-1">
                <Label>Subject</Label>
                <Input value={subject} onChange={(e) => setSubject(e.target.value)} />
              </div>
            )}
            <div className="space-y-1">
              <Label>Body</Label>
              <Textarea
                rows={8}
                value={body}
                onChange={(e) => setBody(e.target.value)}
              />
            </div>
            {preview && (
              <div className="rounded border bg-muted p-3 text-sm">
                <p className="mb-1 font-medium">Preview:</p>
                <p>{preview}</p>
              </div>
            )}
            <div className="flex justify-between">
              <div className="flex gap-2">
                <Button variant="outline" onClick={handlePreview}>
                  Preview
                </Button>
                <Button variant="destructive" onClick={handleReset}>
                  Reset to Default
                </Button>
              </div>
              <Button onClick={handleSave}>Save</Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  )
}

export default function NotificationsPage() {
  const { data: configs, isLoading: configsLoading } = useNotificationConfigs()
  const { data: templates, isLoading: templatesLoading } =
    useNotificationTemplates()

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">Notifications</h1>
        <p className="text-muted-foreground">
          Manage notification channels and message templates.
        </p>
      </div>

      <Tabs defaultValue="channels">
        <TabsList>
          <TabsTrigger value="channels">Channels</TabsTrigger>
          <TabsTrigger value="templates">Templates</TabsTrigger>
        </TabsList>

        <TabsContent value="channels" className="space-y-4">
          {configsLoading && <p>Loading...</p>}
          {configs?.map((config) => (
            <ChannelCard key={config.id} config={config} />
          ))}
        </TabsContent>

        <TabsContent value="templates" className="space-y-4">
          {templatesLoading && <p>Loading...</p>}
          {templates?.map((template) => (
            <TemplateEditor key={template.id} template={template} />
          ))}
        </TabsContent>
      </Tabs>
    </div>
  )
}
