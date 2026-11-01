import { errorMessage } from "@/lib/api"

// The API's answers about passwords, in the app's language.
export function passwordError(e: unknown) {
  const message = errorMessage(e)
  if (message.startsWith("The new password must be different"))
    return "A nova palavra-passe tem de ser diferente da atual."
  if (message.startsWith("The current password is not correct")) return "A palavra-passe atual não está correta."
  return message
}
