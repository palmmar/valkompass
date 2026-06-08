// Speglar backend-DTO:erna (camelCase via System.Text.Json Web-defaults).

export interface QuestionnaireCategory {
  slug: string;
  name: string;
  description: string | null;
  displayOrder: number;
}

export interface QuestionnaireQuestion {
  id: number;
  text: string;
  explanation: string | null;
  explanationSourceUrl: string | null;
  categorySlug: string;
}

export interface Questionnaire {
  categories: QuestionnaireCategory[];
  questions: QuestionnaireQuestion[];
}

export interface SubmitAnswer {
  questionId: number;
  value: number | null;
  isSkipped: boolean;
  isImportant: boolean;
}

export interface SubmitQuizRequest {
  answers: SubmitAnswer[];
}

export interface SubmitQuizResponse {
  shareToken: string;
}

export interface ResultPartyRef {
  code: string;
  name: string;
  fullName: string;
  color: string | null;
  shortDescription: string | null;
}

export interface ResultPartyScore {
  partyCode: string;
  agreementPct: number | null;
  comparedQuestionCount: number;
}

export interface ResultCategory {
  slug: string;
  name: string;
  parties: ResultPartyScore[];
}

export interface ResultQuestionParty {
  partyCode: string;
  partyValue: number | null;
  agreementPct: number | null;
  motivation: string | null;
  sourceCitation: string | null;
  sourceUrl: string | null;
}

export interface ResultQuestion {
  questionId: number;
  externalKey: string;
  text: string;
  explanation: string | null;
  explanationSourceUrl: string | null;
  categorySlug: string;
  categoryName: string;
  userValue: number | null;
  skipped: boolean;
  isImportant: boolean;
  parties: ResultQuestionParty[];
}

export interface ResultDocument {
  createdAt: string;
  parties: ResultPartyRef[];
  overall: ResultPartyScore[];
  categories: ResultCategory[];
  questions: ResultQuestion[];
  disclaimer: string;
}
