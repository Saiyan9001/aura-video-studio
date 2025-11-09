import axios from 'axios';
import type { DocumentInfo, IndexStatistics, IndexingResult, Citation } from '../types';
import { env } from '../config/env';

const API_BASE_URL = env.apiBaseUrl;

export interface SearchRequest {
  query: string;
  topK?: number;
  minimumScore?: number;
  maxContextTokens?: number;
  includeCitations?: boolean;
}

export interface RagContext {
  query: string;
  formattedContext: string;
  chunks: Array<{
    content: string;
    source: string;
    section?: string;
    pageNumber?: number;
    relevanceScore: number;
    citationNumber: number;
  }>;
  citations: Citation[];
  totalTokens: number;
}

export const ragClient = {
  async uploadDocument(
    file: File,
    strategy: 'Semantic' | 'Fixed' | 'Sentence' | 'Paragraph' = 'Semantic',
    maxChunkSize: number = 512
  ): Promise<IndexingResult> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await axios.post<IndexingResult>(`${API_BASE_URL}/api/rag/ingest`, formData, {
      params: { strategy, maxChunkSize },
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return response.data;
  },

  async getDocuments(): Promise<DocumentInfo[]> {
    const response = await axios.get<DocumentInfo[]>(`${API_BASE_URL}/api/rag/documents`);
    return response.data;
  },

  async getStatistics(): Promise<IndexStatistics> {
    const response = await axios.get<IndexStatistics>(`${API_BASE_URL}/api/rag/statistics`);
    return response.data;
  },

  async search(request: SearchRequest): Promise<RagContext> {
    const response = await axios.post<RagContext>(`${API_BASE_URL}/api/rag/search`, request);
    return response.data;
  },

  async deleteDocument(documentId: string): Promise<void> {
    await axios.delete(`${API_BASE_URL}/api/rag/documents/${documentId}`);
  },

  async clearAll(): Promise<void> {
    await axios.delete(`${API_BASE_URL}/api/rag/clear`);
  },
};
