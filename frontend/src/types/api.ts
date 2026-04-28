export type Direction = 'Positive' | 'Negative' | 'Neutral' | 'Uncertain' | string;

export interface PostAnalysis {
  id: number;
  postId: number;
  marketImpactScore: number;
  direction: Direction;
  reasoning: string;
  affectedAssetsJson?: string | null;
  confidence?: number | null;
  analyzerVersion: string;
  rawAiResponse?: string | null;
  analyzedAt: string;
  createdAt: string;
}

export interface TruthPost {
  id: number;
  source: string;
  author: string;
  externalId: string;
  url: string;
  content: string;
  createdAt: string;
  collectedAt: string;
  savedAtUtc: string;
  hasImage: boolean;
  imageUrls: string[];
  raw?: unknown;
  analysis?: PostAnalysis | null;
}

export interface Alert {
  id: number;
  postId: number;
  postAnalysisId: number;
  alertType: string;
  recipient: string;
  subject: string;
  body: string;
  threshold: number;
  sentAt?: string | null;
  sendStatus: string;
  errorMessage?: string | null;
  createdAt: string;
}

export interface AnalysisRunResult {
  analyzedCount: number;
  skippedCount: number;
  failedCount: number;
  message: string;
  analyzedPostIds: number[];
  failedPostIds: number[];
}

export interface CollectorRunTestResult {
  success: boolean;
  message: string;
  fetchedPosts?: number | null;
  savedPosts?: number | null;
  timestamp: string;
  exitCode: number;
  timedOut: boolean;
  stdout: string;
  stderr: string;
}
