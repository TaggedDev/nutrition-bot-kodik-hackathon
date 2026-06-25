import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

type NutritionFacts = {
  calories: number
  protein: number
  fat: number
  carbs: number
}

type ProductNutrition = {
  productId: string
  productName: string
  brand: string | null
  nutritionFacts: NutritionFacts
  sourceType: string
  sourceReference: string
  confidenceScore: number
}


function App() {
  const [query, setQuery] = useState('')
  const [items, setItems] = useState<ProductNutrition[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const hasResults = items.length > 0

  const title = useMemo(() => {
    if (loading) {
      return 'Идет поиск...'
    }

    if (error) {
      return error
    }

    if (hasResults) {
      return `Найдено товаров: ${items.length}`
    }

    return 'Введите продукт и нажмите "Искать"'
  }, [error, hasResults, items.length, loading])

  const handleSearch = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    const trimmedQuery = query.trim()
    if (!trimmedQuery) {
      setError('Введите поисковый запрос.')
      setItems([])
      return
    }

    try {
      setLoading(true)
      setError(null)

      const response = await fetch(
        `/api/v1/kbju/search?query=${encodeURIComponent(trimmedQuery)}`,
      )

      if (!response.ok) {
        throw new Error(`Ошибка API: ${response.status}`)
      }

      const payload = (await response.json()) as unknown
      if (!Array.isArray(payload)) {
        throw new Error('Некорректный формат ответа API.')
      }

      const typedPayload = payload as ProductNutrition[]
      setItems(typedPayload)

      if (typedPayload.length === 0) {
        setError('Ничего не найдено. Попробуйте другой запрос.')
      }
    } catch (error) {
      setItems([])
      if (error instanceof Error) {
        setError(`Не удалось выполнить поиск: ${error.message}`)
      } else {
        setError('Не удалось выполнить поиск. Проверьте, что backend запущен.')
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="page">
      <h1>Поиск КБЖУ товаров</h1>

      <form className="search-form" onSubmit={handleSearch}>
        <input
          type="text"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Например: макароны, milk, chocolate"
          aria-label="Поисковый запрос"
        />
        <button type="submit" disabled={loading}>
          {loading ? 'Ищем...' : 'Искать'}
        </button>
      </form>

      <p className="status">{title}</p>

      {hasResults && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Название</th>
                <th>Бренд</th>
                <th>Ккал</th>
                <th>Белки</th>
                <th>Жиры</th>
                <th>Углеводы</th>
                <th>Источник</th>
                <th>Confidence</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={`${item.productId}-${item.sourceReference}`}>
                  <td>{item.productName}</td>
                  <td>{item.brand ?? '-'}</td>
                  <td>{item.nutritionFacts.calories}</td>
                  <td>{item.nutritionFacts.protein}</td>
                  <td>{item.nutritionFacts.fat}</td>
                  <td>{item.nutritionFacts.carbs}</td>
                  <td>{item.sourceType}</td>
                  <td>{item.confidenceScore.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </main>
  )
}

export default App
