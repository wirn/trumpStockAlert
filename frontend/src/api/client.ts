import type { AnalysisRunResult, CollectorRunResult, FetcherRun, PostAnalysis, TruthPost } from '../types/api';

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
const apiBaseUrl = (configuredBaseUrl?.trim() || 'http://localhost:5044').replace(/\/$/, '');
const schedulerApiKey = import.meta.env.VITE_SCHEDULER_API_KEY as string | undefined;

export class ApiRequestError extends Error {
  readonly status?: number;
  readonly responseBody?: string;

  constructor(
    message: string,
    status?: number,
    responseBody?: string
  ) {
    super(message);
    this.name = 'ApiRequestError';
    this.status = status;
    this.responseBody = responseBody;
  }
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;

  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      headers: {
        Accept: 'application/json',
        ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
        ...init?.headers,
      },
      ...init,
    });
  } catch (error) {
    throw new ApiRequestError(error instanceof Error ? `Network error: ${error.message}` : 'Network error while calling the API.');
  }

  if (!response.ok) {
    const body = await response.text();
    const message = response.status === 401
      ? 'Unauthorized: scheduler API key is missing or invalid.'
      : `${response.status} ${response.statusText}${body ? `: ${body}` : ''}`;
    throw new ApiRequestError(message, response.status, body);
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

export async function runCollector(): Promise<CollectorRunResult> {
  const apiKey = schedulerApiKey?.trim();
  if (!apiKey) {
    throw new ApiRequestError('VITE_SCHEDULER_API_KEY is not configured for the manual collector trigger.');
  }

  return requestJson<CollectorRunResult>('/api/collector/run', {
    method: 'POST',
    headers: {
      'X-TrumpStockAlert-Scheduler-Key': apiKey,
    },
  });
}

export async function getLatestFetcherRuns(): Promise<FetcherRun[]> {
  return requestJson<FetcherRun[]>('/api/fetcher-runs/latest');
}

export function getApiBaseUrl(): string {
  return apiBaseUrl;
}
