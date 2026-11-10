// Form rules shared by the screens. Messages say what is wrong and how to fix it (report, section 16.2).

export const isEmail = (value: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim())

export function emailError(value: string) {
  if (!value.trim()) return "Introduza o email."
  return isEmail(value) ? null : "Introduza um email válido (ex.: nome@exemplo.pt)."
}

// At least 8 characters with a number, as the prototype explains when creating an account.
export function newPasswordError(value: string) {
  if (value.length < 8 || !/\d/.test(value)) return "Mínimo de 8 caracteres, com pelo menos um número."
  return null
}

export const required = (value: string, message: string) => (value.trim() ? null : message)

// "2,40" or "2.40" → 2.4; null when it is not a positive number.
export function parseDecimal(value: string): number | null {
  const normalized = value.trim().replace(/\s|€/g, "").replace(",", ".")
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) return null
  return Number(normalized)
}

export function parseInteger(value: string): number | null {
  const normalized = value.trim()
  return /^\d+$/.test(normalized) ? Number(normalized) : null
}

// "31/10/2026" → a date at the end of that day; null when it is not a valid date.
export function parseDate(value: string): Date | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value.trim())
  if (!match) return null
  const [, day, month, year] = match.map(Number) as [number, number, number, number]
  const date = new Date(year, month - 1, day, 23, 59, 0)
  return date.getDate() === day && date.getMonth() === month - 1 ? date : null
}
