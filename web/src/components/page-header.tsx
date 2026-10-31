import { ArrowLeft } from "lucide-react"
import type { ReactNode } from "react"
import { useNavigate } from "react-router-dom"
import { Button } from "@/components/ui/button"

export function PageHeader({
  title,
  description,
  actions,
  back,
}: {
  title: string
  description?: ReactNode
  actions?: ReactNode
  back?: boolean
}) {
  const navigate = useNavigate()
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div className="flex min-w-0 items-start gap-2">
        {back && (
          <Button variant="ghost" size="icon" onClick={() => navigate(-1)} aria-label="Voltar" className="-ml-3">
            <ArrowLeft className="size-5" />
          </Button>
        )}
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold sm:text-[28px] sm:leading-9">{title}</h1>
          {description && <p className="mt-1 text-muted-foreground">{description}</p>}
        </div>
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}
