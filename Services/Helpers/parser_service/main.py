from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
import httpx, re
from selectolax.parser import HTMLParser
import uvicorn

app = FastAPI()
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                  "(KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
    "Accept-Language": "ru-RU,ru;q=0.9",
    "Accept-Encoding": "gzip, deflate, br",
}

# Категории продуктов price.ru (slug → отображаемое имя)
FOOD_CATEGORIES = {
    "produkty/molochnye-produkty":         "🥛 Молочные продукты",
    "produkty/myaso-i-ptitsa":             "🥩 Мясо и птица",
    "produkty/ryba-i-moreprodukty":        "🐟 Рыба и морепродукты",
    "produkty/ovoshchi-i-frukty":          "🍎 Овощи и фрукты",
    "produkty/hleb-i-vypechka":            "🍞 Хлеб и выпечка",
    "produkty/bakalejnye-tovary":          "🌾 Бакалея",
    "produkty/konditerskie-izdeliya":      "🍫 Кондитерские изделия",
    "produkty/napitki":                    "🧃 Напитки",
    "produkty/zamorozhennye-produkty":     "🧊 Заморозка",
    "produkty/kolbasy-i-delikatesy":       "🥓 Колбасы и деликатесы",
    "produkty/masla-i-zhiry":              "🧈 Масла и жиры",
    "produkty/soуsy-i-spetsii":            "🌶️ Соусы и специи",
    "produkty/detskoe-pitanie":            "👶 Детское питание",
    "produkty/sportivnoe-pitanie":         "💪 Спортивное питание",
    "produkty":                            "🛒 Все продукты",
}

@app.get("/categories")
def get_categories():
    return [{"slug": k, "name": v} for k, v in FOOD_CATEGORIES.items()]

@app.get("/products")
async def get_products(slug: str, page: int = 1):
    url = f"https://price.ru/{slug}/"
    params = {}
    if page > 1:
        params["page"] = page

    async with httpx.AsyncClient(headers=HEADERS, follow_redirects=True, timeout=20) as client:
        try:
            r = await client.get(url, params=params)
        except Exception as e:
            raise HTTPException(502, f"Сеть: {e}")

    if r.status_code != 200:
        raise HTTPException(r.status_code, f"price.ru ответил {r.status_code}")

    html = HTMLParser(r.text)
    products = []

    # Основные селекторы price.ru (проверены на живом HTML)
    # Карточки товаров: .c-grid-item, [class*="ProductItem"], [class*="product-item"]
    cards = (html.css(".c-grid-item") or
             html.css("[class*='ProductItem']") or
             html.css("[class*='product-item']") or
             html.css("article") or
             html.css(".item"))

    for card in cards:
        # Название
        name_el = (card.css_first("h3") or
                   card.css_first("h2") or
                   card.css_first("[class*='name']") or
                   card.css_first("[class*='title']"))
        name = name_el.text(strip=True) if name_el else None
        if not name or len(name) < 3:
            continue

        # Минимальная цена
        price_el = (card.css_first("[class*='price']") or
                    card.css_first("[class*='Price']"))
        price_raw = price_el.text(strip=True) if price_el else ""
        price_str = re.sub(r"[^\d]", "", price_raw.split("–")[0].split("-")[0])
        price = int(price_str) if price_str else None

        # Картинка
        img_el = card.css_first("img")
        img = None
        if img_el:
            img = (img_el.attributes.get("data-src") or
                   img_el.attributes.get("src") or "")
            if img.startswith("//"):
                img = "https:" + img
            elif img.startswith("/"):
                img = "https://price.ru" + img
            if "placeholder" in img or "logo" in img:
                img = None

        # Ссылка на страницу товара
        link_el = card.css_first("a")
        link = None
        if link_el:
            href = link_el.attributes.get("href", "")
            link = ("https://price.ru" + href
                    if href.startswith("/") else href)

        # Количество предложений
        offers_el = card.css_first("[class*='offer'], [class*='shop']")
        offers = None
        if offers_el:
            m = re.search(r"\d+", offers_el.text())
            if m:
                offers = int(m.group())

        products.append({
            "name":   name,
            "price":  price,
            "img":    img,
            "link":   link,
            "offers": offers,
        })

    # Пагинация
    has_next = bool(
        html.css_first(".pagination__next") or
        html.css_first("[class*='next']") or
        html.css_first("[aria-label='Следующая']")
    )

    return {
        "products": products,
        "has_next": has_next,
        "page": page,
        "total": len(products),
    }

@app.get("/search")
async def search_products(q: str, page: int = 1):
    """Поиск товаров по названию"""
    url = "https://price.ru/search/"
    params = {"text": q}
    if page > 1:
        params["page"] = page

    async with httpx.AsyncClient(headers=HEADERS, follow_redirects=True, timeout=20) as client:
        try:
            r = await client.get(url, params=params)
        except Exception as e:
            raise HTTPException(502, f"Сеть: {e}")

    if r.status_code != 200:
        raise HTTPException(r.status_code, f"price.ru ответил {r.status_code}")

    # Используем тот же парсер
    return await _parse_listing(r.text, page)

async def _parse_listing(html_text: str, page: int):
    html = HTMLParser(html_text)
    products = []

    cards = (html.css(".c-grid-item") or
             html.css("[class*='ProductItem']") or
             html.css("[class*='product-item']") or
             html.css("article"))

    for card in cards:
        name_el = card.css_first("h3") or card.css_first("h2") or card.css_first("[class*='name']")
        name = name_el.text(strip=True) if name_el else None
        if not name or len(name) < 3:
            continue

        price_el = card.css_first("[class*='price']") or card.css_first("[class*='Price']")
        price_raw = price_el.text(strip=True) if price_el else ""
        price_str = re.sub(r"[^\d]", "", price_raw.split("–")[0])
        price = int(price_str) if price_str else None

        img_el = card.css_first("img")
        img = None
        if img_el:
            src = img_el.attributes.get("data-src") or img_el.attributes.get("src") or ""
            if src.startswith("//"):
                src = "https:" + src
            elif src.startswith("/"):
                src = "https://price.ru" + src
            if src and "placeholder" not in src and "logo" not in src:
                img = src

        link_el = card.css_first("a")
        href = link_el.attributes.get("href", "") if link_el else ""
        link = "https://price.ru" + href if href.startswith("/") else href

        products.append({"name": name, "price": price, "img": img, "link": link})

    has_next = bool(html.css_first("[class*='next']") or html.css_first(".pagination__next"))
    return {"products": products, "has_next": has_next, "page": page, "total": len(products)}

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8765)