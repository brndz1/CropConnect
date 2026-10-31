export function FieldError({ message }: { message?: string }) {
  if (!message) return null
  return (
    <p role="alert" className="flex items-center gap-1.5 text-sm text-destructive">
      <span
        aria-hidden
        className="flex size-4 items-center justify-center rounded-full bg-destructive text-[10px] font-bold text-white"
      >
        !
      </span>
      {message}
    </p>
  )
}
