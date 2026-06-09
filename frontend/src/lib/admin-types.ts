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
  isActive: boolean;
}
export interface QuestionInput {
  externalKey: string;
  text: string;
  explanation: string | null;
  explanationSourceUrl: string | null;
  categoryId: number;
  displayOrder: number;
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
