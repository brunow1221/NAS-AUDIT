export type DataMode = 'Historical' | 'Demo' | 'Live'
export type AuditAction = 'Unknown' | 'Creation' | 'Open' | 'Write' | 'Deletion' | 'Rename' | 'Move' | 'Connection' | 'ObservedRead'

export interface ApiEnvelope<T> {
  data: T
  message?: string
}

export interface UserSession {
  email: string
  displayName: string
  roles: string[]
  mustChangePassword: boolean
}

export interface DashboardSummary {
  totalEvents: number
  eventsLast24Hours: number
  activeAccounts: number
  createdFiles: number
  modifiedFiles: number
  deletions: number
  renames: number
  connections: number
  lastEventUtc: string | null
  collectorStatus: string
}

export interface ActivityPoint { bucketUtc: string; count: number }
export interface DistributionPoint { action: AuditAction; count: number }
export interface TopUser { user: string; count: number }
export interface ShareActivity { share: string; count: number }

export interface CollectorStatus {
  status: 'not_configured' | 'backend_without_agent' | 'connected' | 'active_no_events' | 'disconnected' | 'source_unavailable' | 'recovering' | 'error' | 'database_unavailable' | 'backend_unavailable'
  label: string
  collectorId: string | null
  lastHeartbeatUtc: string | null
  lastEventUtc: string | null
  confirmedCursor: string | null
  queueDepth: number
  message: string | null
}

export interface AuditEvent {
  id: string
  eventTimestampUtc: string
  smbUser: string | null
  clientIp: string | null
  operation: string
  interpretedAction: AuditAction
  interpretationConfidence: 'Unknown' | 'Low' | 'Medium' | 'Confirmed'
  operationResult: string
  shareName: string | null
  filePath: string | null
  sourcePath: string | null
  destinationPath: string | null
  fileExtension: string | null
  objectType: string | null
  isAuxiliaryStream: boolean
  isRecyclePath: boolean
  rawMessage: string
  sourceName: string
  sourceKind: string
  journalCursor: string | null
  bootId: string | null
  metadataJson: string
  dataMode: DataMode
  parserVersion: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface ImportJob {
  id: string
  fileName: string
  status: 'Processing' | 'Completed' | 'CompletedWithWarnings' | 'Failed' | 'Duplicate'
  rowsProcessed: number
  rowsInserted: number
  rowsDuplicate: number
  rowsRejected: number
  warnings: string | null
  startedAtUtc: string
  completedAtUtc: string | null
}

export interface CsvPreview {
  fileName: string
  encoding: string
  delimiter: string
  exportType: string
  headers: string[]
  rows: Record<string, string>[]
  sampledRows: number
  validRows: number
  rejectedRows: number
  warnings: string[]
}

export interface PathSummary {
  path: string
  share: string | null
  eventCount: number
  accountCount: number
  firstSeenUtc: string
  lastSeenUtc: string
  createdAtUtc: string | null
}

export interface PathTimeline {
  summary: PathSummary
  possiblyDeleted: boolean
  renamedOrMovedAway: boolean
  lastKnownOperation: string | null
  events: AuditEvent[]
}

export interface FolderSummary extends PathSummary { name: string }

export interface FolderHistory {
  summary: FolderSummary
  pathSampleTruncated: boolean
  subfolders: FolderSummary[]
  actions: NamedCount[]
  accounts: NamedCount[]
  recentEvents: AuditEvent[]
}

export interface SmbUserSummary {
  user: string
  eventCount: number
  ipCount: number
  shareCount: number
  firstSeenUtc: string
  lastSeenUtc: string
  lastClientIp: string | null
}

export interface NamedCount { name: string; count: number }
export interface DatedCount { dayUtc: string; count: number }

export interface SmbUserActivity {
  summary: SmbUserSummary
  actions: NamedCount[]
  shares: NamedCount[]
  clientIps: NamedCount[]
  activityByDay: DatedCount[]
  recentEvents: AuditEvent[]
}

export interface DatedActionCount { dayUtc: string; action: AuditAction; count: number }
export interface FolderActivity { folder: string; share: string | null; eventCount: number; accountCount: number; lastSeenUtc: string }

export interface Statistics {
  totalEvents: number
  activeDays: number
  averageEventsPerActiveDay: number
  folderAnalysisTruncated: boolean
  trend: DatedActionCount[]
  extensions: NamedCount[]
  results: NamedCount[]
  folders: FolderActivity[]
}

export interface ActivityGroup {
  groupId: string
  startedAtUtc: string
  endedAtUtc: string
  user: string | null
  clientIp: string | null
  share: string | null
  path: string | null
  eventCount: number
  operations: string[]
  eventIds: string[]
}

export interface AlertCandidate {
  ruleKey: string
  severity: 'info' | 'warning' | 'critical'
  title: string
  windowStartUtc: string
  windowEndUtc: string
  observedCount: number
  explanation: string
  dimensions: Record<string, string>
}

export interface RetentionPolicy {
  enabled: boolean
  historicalDays: number
  liveDays: number
  demoDays: number
  ingestionMetadataDays: number
  rejectedEventDays: number
  batchSize: number
  runAtUtcHour: number
  lastRunUtc: string | null
  lastRunStatus: string | null
}

export interface RetentionPreview {
  policy: RetentionPolicy
  events: Array<{ dataMode: DataMode; cutoffUtc: string; eligibleRows: number }>
  ingestionMetadataCutoffUtc: string
  eligibleIngestionBatches: number
  rejectedEventCutoffUtc: string
  eligibleRejectedEvents: number
}

export interface OperationalEvent {
  timestampUtc: string
  severity: string
  code: string
  message: string
}

export interface SystemStatus {
  checkedAtUtc: string
  version: string
  uptimeSeconds: number
  apiStatus: string
  databaseStatus: string
  databaseLatencyMs: number
  databaseSizeBytes: number
  totalEvents: number
  lastEventUtc: string | null
  collector: CollectorStatus
  ingestion: {
    lastBatchUtc: string | null
    batchesLast24Hours: number
    acceptedLast24Hours: number
    duplicatesLast24Hours: number
    rejectedLast24Hours: number
    pendingEvents: number
  }
  retention: RetentionPolicy
  alerts: AlertCandidate[]
  recentErrors: OperationalEvent[]
}

export interface ApplicationUser {
  id: string
  email: string
  displayName: string
  role: 'Administrator' | 'Auditor' | 'Sin rol'
  isEnabled: boolean
  mustChangePassword: boolean
  createdAtUtc: string
  lastLoginAtUtc: string | null
  lastPasswordChangedAtUtc: string | null
}

