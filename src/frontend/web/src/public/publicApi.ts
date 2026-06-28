import { apiBaseUrl } from "../authConfig";

/** A program as exposed to anonymous landing-page visitors (public-safe — no honorarium / preceptor /
 *  description). Mirrors the backend PublicProgramResponse. */
export interface PublicProgram {
  id: string;
  programNumber: number;
  specialtyName: string;
  programType: string;
  city: string | null;
  state: string | null;
  retailAmountPerWeek: number;
  minWeeksPerRotation: number;
  instantApproval: boolean;
  imageUrl: string | null;
}

/** Fetch the anonymous public program feed for the landing hero. No auth token — this endpoint is
 *  AllowAnonymous on the API. Returns [] on any error so the landing never hard-fails on the feed. */
export async function getPublicPrograms(signal?: AbortSignal): Promise<PublicProgram[]> {
  try {
    const res = await fetch(`${apiBaseUrl}/api/public/programs`, { signal });
    if (!res.ok) return [];
    return (await res.json()) as PublicProgram[];
  } catch {
    return [];
  }
}
