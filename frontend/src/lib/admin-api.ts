import type {
  AdminCategory,
  AdminParty,
  AdminQuestion,
  AuthUser,
  CategoryInput,
  PartyInput,
  PositionInput,
  PositionsMatrix,
  QuestionInput,
  QuizStats,
  AnswerStats,
  PartyMatchStats,
} from "@/lib/admin-types";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

async function req<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`/api${path}`, {
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    ...options,
  });

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const data = text ? JSON.parse(text) : null;

  if (!res.ok) {
    let message = data?.message ?? `Något gick fel (${res.status}).`;
    if (data?.errors) message = Object.values(data.errors).flat().join(" ");
    throw new ApiError(res.status, message);
  }
  return data as T;
}

// --- Auth ---
export const login = (email: string, password: string) =>
  req<AuthUser>("/auth/login", { method: "POST", body: JSON.stringify({ email, password }) });
export const logout = () => req<void>("/auth/logout", { method: "POST" });
export const getMe = () => req<AuthUser>("/auth/me");

// --- Categories ---
export const listCategories = () => req<AdminCategory[]>("/admin/categories");
export const createCategory = (input: CategoryInput) =>
  req<AdminCategory>("/admin/categories", { method: "POST", body: JSON.stringify(input) });
export const updateCategory = (id: number, input: CategoryInput) =>
  req<AdminCategory>(`/admin/categories/${id}`, { method: "PUT", body: JSON.stringify(input) });
export const deleteCategory = (id: number) =>
  req<void>(`/admin/categories/${id}`, { method: "DELETE" });

// --- Parties ---
export const listParties = () => req<AdminParty[]>("/admin/parties");
export const createParty = (input: PartyInput) =>
  req<AdminParty>("/admin/parties", { method: "POST", body: JSON.stringify(input) });
export const updateParty = (id: number, input: PartyInput) =>
  req<AdminParty>(`/admin/parties/${id}`, { method: "PUT", body: JSON.stringify(input) });
export const deleteParty = (id: number) =>
  req<void>(`/admin/parties/${id}`, { method: "DELETE" });

export const uploadPartyLogo = async (id: number, file: File): Promise<void> => {
  const body = new FormData();
  body.append("file", file); // fältnamn "file" matchar IFormFile-parametern i backend
  // Sätt INTE Content-Type själv – webbläsaren lägger till multipart-gränsen.
  const res = await fetch(`/api/admin/parties/${id}/logo`, {
    method: "POST",
    credentials: "include",
    body,
  });
  if (!res.ok) {
    const text = await res.text();
    let message = `Något gick fel (${res.status}).`;
    try {
      const data = text ? JSON.parse(text) : null;
      if (data?.errors) message = Object.values(data.errors).flat().join(" ");
      else if (data?.message) message = data.message;
    } catch {
      /* behåll standardmeddelandet */
    }
    throw new ApiError(res.status, message);
  }
};

export const deletePartyLogo = (id: number) =>
  req<void>(`/admin/parties/${id}/logo`, { method: "DELETE" });

// --- Questions ---
export const listQuestions = () => req<AdminQuestion[]>("/admin/questions");
export const getQuestion = (id: number) => req<AdminQuestion>(`/admin/questions/${id}`);
export const createQuestion = (input: QuestionInput) =>
  req<AdminQuestion>("/admin/questions", { method: "POST", body: JSON.stringify(input) });
export const updateQuestion = (id: number, input: QuestionInput) =>
  req<AdminQuestion>(`/admin/questions/${id}`, { method: "PUT", body: JSON.stringify(input) });
export const deleteQuestion = (id: number) =>
  req<void>(`/admin/questions/${id}`, { method: "DELETE" });

// --- Positions ---
export const getPositions = (questionId: number) =>
  req<PositionsMatrix>(`/admin/questions/${questionId}/positions`);
export const savePositions = (questionId: number, positions: PositionInput[]) =>
  req<void>(`/admin/questions/${questionId}/positions`, {
    method: "PUT",
    body: JSON.stringify({ positions }),
  });

// --- Statistik ---
export const getQuizStats = () => req<QuizStats>("/admin/quiz/stats");
export const getAnswerStats = () => req<AnswerStats>("/admin/quiz/answer-stats");
export const getPartyMatchStats = () => req<PartyMatchStats>("/admin/quiz/party-stats");
