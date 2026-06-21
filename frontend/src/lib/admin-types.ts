export interface AuthUser {
  email: string;
  roles: string[];
}

export interface AdminCategory {
  id: number;
  slug: string;
  name: string;
  description: string | null;
  icon: string | null;
  displayOrder: number;
}
export type CategoryInput = Omit<AdminCategory, "id">;

export interface AdminParty {
  id: number;
  code: string;
  name: string;
  fullName: string;
  shortDescription: string | null;
  color: string | null;
  displayOrder: number;
  isActive: boolean;
  hasLogo: boolean;
}
export type PartyInput = Omit<AdminParty, "id" | "hasLogo">;

export interface AdminQuestion {
  id: number;
  externalKey: string;
  text: string;
  explanation: string | null;
  explanationSourceUrl: string | null;
  categoryId: number;
  categorySlug: string;
  categoryName: string;
  displayOrder: number;
  tier: number;
  isActive: boolean;
}
export interface QuestionInput {
  externalKey: string;
  text: string;
  explanation: string | null;
  explanationSourceUrl: string | null;
  categoryId: number;
  displayOrder: number;
  tier: number;
  isActive: boolean;
}

export interface AdminPosition {
  partyId: number;
  partyCode: string;
  partyName: string;
  value: number | null;
  motivation: string | null;
  sourceCitation: string | null;
  sourceUrl: string | null;
}
export interface PositionsMatrix {
  questionId: number;
  questionText: string;
  positions: AdminPosition[];
}
export interface PositionInput {
  partyId: number;
  value: number | null;
  motivation: string | null;
  sourceCitation: string | null;
  sourceUrl: string | null;
}

export interface QuizSessionSummary {
  id: string;
  completedAt: string;
}
export interface QuizStats {
  total: number;
  last24h: number;
  last7d: number;
  latest: QuizSessionSummary[];
}

export interface QuestionAnswerStats {
  questionId: number;
  categoryName: string;
  text: string;
  total: number;
  answered: number;
  skipped: number;
  important: number;
  stronglyDisagree: number;
  partlyDisagree: number;
  partlyAgree: number;
  stronglyAgree: number;
  suppressed: boolean;
}
export interface AnswerStats {
  sessions: number;
  questions: QuestionAnswerStats[];
}

export interface PartyMatchSlice {
  partyCode: string;
  count: number;
}
export interface PartyMatchStats {
  sessions: number;
  tied: number;
  slices: PartyMatchSlice[];
}
