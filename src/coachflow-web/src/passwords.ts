export const minPasswordLength = 10

export function passwordMismatch(password: string, confirmation: string) {
  return password === confirmation ? null : 'Die beiden Passwörter stimmen nicht überein.'
}
