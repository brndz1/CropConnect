import { useMutation, useQueryClient } from "@tanstack/react-query"
import { Loader2 } from "lucide-react"
import { useState, type FormEvent, type ReactNode } from "react"
import { toast } from "sonner"
import { FieldError } from "@/components/field-error"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { Textarea } from "@/components/ui/textarea"
import { api, errorMessage } from "@/lib/api"
import { productTypeLabel } from "@/lib/format"
import type { Product, ProductType } from "@/lib/types"

const toDateInput = (value: string | null) => (value ? value.slice(0, 10) : "")
// The basket stays available until the end of the chosen day.
const fromDateInput = (value: string) => (value ? new Date(`${value}T23:59:00`).toISOString() : null)

// RF14 and RF16: create or edit a product, a pre-defined basket or a surprise basket.
export function ProductFormDialog({
  farmId,
  product,
  defaultType = "Product",
  trigger,
}: {
  farmId: string
  product?: Product
  defaultType?: ProductType
  trigger: ReactNode
}) {
  const queryClient = useQueryClient()
  const editing = !!product
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string>()
  const [form, setForm] = useState(() => initial())

  function initial() {
    return {
      type: product?.type ?? defaultType,
      name: product?.name ?? "",
      description: product?.description ?? "",
      unit: product?.unit ?? (defaultType === "Product" ? "kg" : "cabaz"),
      price: product ? String(product.price).replace(".", ",") : "",
      stockQuantity: product ? String(product.stockQuantity) : "",
      availableUntil: toDateInput(product?.availableUntil ?? null),
      contents: product?.contents ?? "",
      isAvailable: product?.isAvailable ?? true,
    }
  }

  const isBasket = form.type !== "Product"
  const save = useMutation({
    mutationFn: () => {
      const price = Number(form.price.replace(",", "."))
      const common = {
        name: form.name.trim(),
        description: form.description.trim(),
        unit: form.unit.trim(),
        price,
        availableUntil: isBasket ? fromDateInput(form.availableUntil) : null,
        contents: isBasket ? form.contents.trim() : null,
      }
      return editing
        ? api.patch<Product>(`/api/products/${product!.id}`, {
            ...common,
            isAvailable: form.isAvailable,
            expectedVersion: product!.version,
          })
        : api.post<Product>("/api/products", {
            ...common,
            farmId,
            type: form.type,
            stockQuantity: Number(form.stockQuantity),
          })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["products"] })
      toast.success(editing ? "Produto atualizado." : isBasket ? "Cabaz criado." : "Produto criado.")
      setOpen(false)
    },
    onError: (e) => setError(errorMessage(e)),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const price = Number(form.price.replace(",", "."))
    if (!form.name.trim()) return setError("Indique o nome.")
    if (!Number.isFinite(price) || price < 0) return setError("Indique um preço válido.")
    if (!editing && !(Number(form.stockQuantity) >= 0 && form.stockQuantity !== ""))
      return setError("Indique o stock inicial.")
    if (isBasket && !form.availableUntil) return setError("Os cabazes precisam de uma data limite de levantamento.")
    setError(undefined)
    save.mutate()
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setForm(initial())
          setError(undefined)
        }
      }}
    >
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="font-heading text-brand-900">
            {editing ? `Editar ${product!.name}` : isBasket ? "Novo cabaz" : "Novo produto"}
          </DialogTitle>
          <DialogDescription>
            {form.type === "SurpriseBasket"
              ? "O cliente vê o preço, a descrição e a data limite; o conteúdo só é revelado no levantamento."
              : "Os campos com preço e stock aparecem no catálogo."}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="grid gap-4" noValidate>
          {!editing && (
            <div className="grid gap-2">
              <Label htmlFor="product-type">Tipo</Label>
              <Select
                value={form.type}
                onValueChange={(value) =>
                  setForm({ ...form, type: value as ProductType, unit: value === "Product" ? "kg" : "cabaz" })
                }
              >
                <SelectTrigger id="product-type" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(Object.keys(productTypeLabel) as ProductType[]).map((type) => (
                    <SelectItem key={type} value={type}>
                      {productTypeLabel[type]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
          <div className="grid gap-2">
            <Label htmlFor="name">Nome</Label>
            <Input
              id="name"
              maxLength={120}
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="description">Descrição</Label>
            <Textarea
              id="description"
              rows={2}
              maxLength={1000}
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <Label htmlFor="price">Preço (€)</Label>
              <Input
                id="price"
                inputMode="decimal"
                placeholder="2,40"
                value={form.price}
                onChange={(e) => setForm({ ...form, price: e.target.value })}
              />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="unit">Unidade</Label>
              <Input
                id="unit"
                maxLength={20}
                value={form.unit}
                onChange={(e) => setForm({ ...form, unit: e.target.value })}
              />
            </div>
          </div>
          {!editing && (
            <div className="grid gap-2">
              <Label htmlFor="stock">Stock inicial</Label>
              <Input
                id="stock"
                type="number"
                min={0}
                inputMode="numeric"
                value={form.stockQuantity}
                onChange={(e) => setForm({ ...form, stockQuantity: e.target.value })}
              />
            </div>
          )}
          {isBasket && (
            <>
              <div className="grid gap-2">
                <Label htmlFor="until">Levantar até</Label>
                <Input
                  id="until"
                  type="date"
                  min={new Date().toISOString().slice(0, 10)}
                  value={form.availableUntil}
                  onChange={(e) => setForm({ ...form, availableUntil: e.target.value })}
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="contents">
                  Conteúdo{" "}
                  {form.type === "SurpriseBasket" && (
                    <span className="font-normal text-muted-foreground">(só visível para si)</span>
                  )}
                </Label>
                <Textarea
                  id="contents"
                  rows={2}
                  maxLength={1000}
                  placeholder="Abóbora, maçãs, alface…"
                  value={form.contents}
                  onChange={(e) => setForm({ ...form, contents: e.target.value })}
                />
              </div>
            </>
          )}
          {editing && (
            <div className="flex items-center gap-3">
              <Switch
                id="available"
                checked={form.isAvailable}
                onCheckedChange={(checked) => setForm({ ...form, isAvailable: checked })}
              />
              <Label htmlFor="available">Visível no catálogo</Label>
            </div>
          )}
          <FieldError message={error} />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="animate-spin" />}
              {editing ? "Guardar" : "Criar"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
