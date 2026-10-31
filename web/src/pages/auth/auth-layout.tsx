import type { ReactNode } from "react"
import { Link } from "react-router-dom"
import logo from "@/assets/logo.png"

export function AuthLayout({
  title,
  description,
  children,
}: {
  title: string
  description: string
  children: ReactNode
}) {
  return (
    <div className="grid min-h-svh lg:grid-cols-2">
      <div className="hidden flex-col justify-between bg-primary p-12 text-primary-foreground lg:flex">
        <p className="font-heading text-sm font-medium tracking-wide text-white/80 uppercase">CropConnect</p>
        <div>
          <p className="font-heading text-[32px] leading-10 font-bold text-white">Do campo à tua mesa</p>
          <p className="mt-3 max-w-md text-lg text-white/85">
            Encomenda diretamente a quem cultiva, sem intermediários, e levanta na quinta.
          </p>
        </div>
        <p className="text-sm text-white/70">Produtores locais perto de ti</p>
      </div>
      <div className="flex flex-col items-center justify-center px-4 py-10">
        <div className="w-full max-w-sm">
          <Link to="/" className="mx-auto mb-8 block w-40" aria-label="CropConnect, início">
            <img src={logo} alt="CropConnect" className="w-full" />
          </Link>
          <h1 className="text-2xl font-semibold">{title}</h1>
          <p className="mt-1 mb-6 text-muted-foreground">{description}</p>
          {children}
        </div>
      </div>
    </div>
  )
}
