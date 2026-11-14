// Tokens aus E-Mail-Links stehen hinter dem #. Dieser Teil wird nie an den Server gesendet
// und erscheint deshalb weder in Server-Logs noch im Referer.
export function readFragment(): URLSearchParams {
  return new URLSearchParams(window.location.hash.slice(1))
}
