import { MapPinOff } from "lucide-react"
import { Link } from "react-router-dom"
import { EmptyState } from "@/components/states"
import { Button } from "@/components/ui/button"

export function NotFoundPage() {
  return (
    <EmptyState
      icon={MapPinOff}
      title="Página não encontrada"
      description="O endereço não existe ou foi alterado."
      action={
        <Button asChild>
          <Link to="/">Voltar ao início</Link>
        </Button>
      }
    />
  )
}
