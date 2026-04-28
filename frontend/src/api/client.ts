import type { AnalysisRunResult, CollectorRunTestResult, PostAnalysis, TruthPost } from '../types/api';

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
const apiBaseUrl = (configuredBaseUrl?.trim() || 'http://localhost:5044').replace(/\/$/, '');

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
    ...init,
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${response.status} ${response.statusText}${body ? `: ${body}` : ''}`);
  }

  return response.json() as Promise<T>;
}

export async function getTruthPosts(limit = 500): Promise<TruthPost[]> {
  return requestJson<TruthPost[]>(`/api/truth-posts?limit=${limit}`);
}

export async function getAnalyses(limit = 500): Promise<PostAnalysis[]> {
  return requestJson<PostAnalysis[]>(`/api/analyses?limit=${limit}`);
}

export async function runAnalysis(): Promise<AnalysisRunResult> {
  return requestJson<AnalysisRunResult>('/api/analysis/run', { method: 'POST' });
}

export async function runCollectorTestMode(): Promise<CollectorRunTestResult> {
  return requestJson<CollectorRunTestResult>('/api/collector/run-test', { method: 'POST' });
}

export const runCollectorTest = runCollectorTestMode;

export function getApiBaseUrl(): string {
  return apiBaseUrl;
}
