import { expect, test, type Page } from '@playwright/test'

const product = (id: string, name: string, calories = 100) => ({
  productId: id,
  productName: name,
  brand: null,
  nutritionFacts: { calories, protein: 3, fat: 2, carbs: 20 },
  nutritionValueBasis: 'Per100Grams',
  servingSize: 100,
  servingUnit: 'g',
  sourceType: 'WebSearch',
  sourceReference: `https://example.test/${id}`,
  confidenceScore: 0.9,
})

async function register(page: Page) {
  await page.goto('/')
  await page.getByRole('button', { name: 'Регистрация' }).click()
  await page.getByLabel('Имя').fill('E2E')
  await page.getByLabel('Фамилия').fill('User')
  await page.getByLabel('Email').fill(`e2e-${Date.now()}-${Math.random()}@example.test`)
  await page.getByLabel('Пароль').fill('Password1')
  await page.getByRole('button', { name: 'Зарегистрироваться' }).click()
  await expect(page.getByRole('heading', { name: 'NutriMate AI' })).toBeVisible()
}

async function stubSearch(page: Page) {
  await page.route('**/api/v1/nutrition/search?**', async (route) => {
    const query = new URL(route.request().url()).searchParams.get('query') ?? ''
    const compound = query.includes('вареньем')
    const clarifications = compound
      ? [
          { id: 'porridge', originalInput: query, parsedProductName: 'манная каша', question: '', candidates: [1, 2, 3].map((i) => product(`porridge-${i}`, `Каша ${i}`)) },
          { id: 'jam', originalInput: query, parsedProductName: 'клубничное варенье', question: '', candidates: [1, 2, 3].map((i) => product(`jam-${i}`, `Варенье ${i}`, 250)) },
        ]
      : [{ id: query, originalInput: query, parsedProductName: query, question: '', candidates: [product(query.replaceAll(' ', '-'), query)] }]
    await route.fulfill({ json: { query, items: [], clarifications, requiresClarification: true, serviceUnavailable: false } })
  })
}

async function search(page: Page, text: string) {
  await page.getByPlaceholder('Я съел творог 150 грамм').fill(text)
  await page.getByRole('button', { name: 'Отправить сообщение' }).click()
}

test('switches meal context and persists entries in breakfast and lunch', async ({ page }) => {
  await stubSearch(page)
  await register(page)

  await page.getByRole('button', { name: 'Завтрак' }).click()
  await search(page, 'манная каша')
  await page.getByRole('article').filter({ hasText: 'манная каша' }).getByRole('button', { name: 'Добавить' }).click()

  await page.getByRole('button', { name: 'Обед' }).click()
  await search(page, 'творог')
  await page.getByRole('article').filter({ hasText: 'творог' }).getByRole('button', { name: 'Добавить' }).click()

  const dayResponse = await page.request.get(`/api/v1/profile/day?date=${new Date().toISOString().slice(0, 10)}&utcOffsetMinutes=0`)
  expect(dayResponse.ok()).toBeTruthy()
  const day = await dayResponse.json()
  expect(day.meals.find((meal: { mealType: string }) => meal.mealType === 'Breakfast').entries).toHaveLength(1)
  expect(day.meals.find((meal: { mealType: string }) => meal.mealType === 'Lunch').entries).toHaveLength(1)

  await page.reload()
  await page.getByRole('button', { name: 'Завтрак' }).click()
  await page.getByRole('button', { name: 'Раскрыть текущий приём пищи' }).click()
  await expect(page.getByText('манная каша', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Свернуть текущий приём пищи' }).click()
  await page.getByRole('button', { name: 'Обед' }).click()
  await page.getByRole('button', { name: 'Раскрыть текущий приём пищи' }).click()
  await expect(page.getByText('творог', { exact: true })).toBeVisible()
})

test('shows three independent choices for porridge and jam and saves both', async ({ page }) => {
  await stubSearch(page)
  await register(page)
  await page.getByRole('button', { name: 'Завтрак' }).click()
  await search(page, 'манная каша с клубничным вареньем')

  const groups = page.getByTestId('product-result-group')
  await expect(groups).toHaveCount(2)
  await expect(groups.nth(0).getByRole('article')).toHaveCount(3)
  await expect(groups.nth(1).getByRole('article')).toHaveCount(3)
  await groups.nth(0).getByRole('article').first().getByRole('button', { name: 'Добавить' }).click()
  await groups.nth(1).getByRole('article').first().getByRole('button', { name: 'Добавить' }).click()

  const dayResponse = await page.request.get(`/api/v1/profile/day?date=${new Date().toISOString().slice(0, 10)}&utcOffsetMinutes=0`)
  const day = await dayResponse.json()
  expect(day.meals.find((meal: { mealType: string }) => meal.mealType === 'Breakfast').entries).toHaveLength(2)
})
