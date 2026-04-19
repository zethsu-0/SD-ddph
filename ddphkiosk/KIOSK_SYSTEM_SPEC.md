# Kiosk Database Spec

## Goal

Make kiosk follow database only.

Separate app is fine.

Use this as contract.

## Firebase Base

```txt
https://dreamdoughph-88e46-default-rtdb.asia-southeast1.firebasedatabase.app
```

## Database Paths

- `/products`
- `/categories`
- `/orders`
- `/walk-in-orders`
- `/customItems`

## Kiosk Read Rules

Kiosk should read:

- `/products`
- `/categories`

Kiosk should not need repo files.

## Products Schema

Path:

```txt
/products/{productId}
```

Shape:

```json
{
  "name": "Chocolate Cake",
  "category": "Cakes",
  "price": 450,
  "image": "https://...",
  "createdAt": "2026-04-19T00:00:00.0000000Z",
  "updatedAt": "2026-04-19T00:00:00.0000000Z"
}
```

Rules:

- key = product id
- `name` = product name
- `category` = category label
- `price` = selling price
- `image` = image URL
- if `category` empty, use `Uncategorized`
- if `image` empty, use placeholder

## Categories Schema

Path:

```txt
/categories/{categoryKey}
```

Shape:

```json
{
  "name": "Cakes",
  "order": 999,
  "protected": false
}
```

Rules:

- key = category key
- `name` = display label
- `order` = optional display order
- if categories missing, build from products

## Kiosk Write Rule

Kiosk should write to:

```txt
/orders
```

Kiosk should not write to:

```txt
/walk-in-orders
```

## Orders Schema

Path:

```txt
/orders/{orderId}
```

Required shape:

```json
{
  "customerName": "Jane Doe",
  "customerPhone": "09123456789",
  "customerEmail": "jane@email.com",
  "pickupDate": "Apr 20, 2026",
  "pickupTime": "03:00 PM",
  "items": [
    {
      "productId": "-abc123",
      "name": "Chocolate Cake",
      "category": "Cakes",
      "quantity": 1,
      "price": 450,
      "subtotal": 450
    }
  ],
  "notes": "Happy birthday note",
  "subtotal": 450,
  "total": 450,
  "status": "pending",
  "orderType": "kiosk",
  "orderSource": "kiosk",
  "paymentStatus": "unpaid",
  "createdAt": "2026-04-19T00:00:00.0000000Z",
  "updatedAt": "2026-04-19T00:00:00.0000000Z",
  "date": "Apr 19, 2026, 04:30 PM"
}
```

## Order Item Schema

Path:

```txt
/orders/{orderId}/items[]
```

Each item must contain:

- `productId`
- `name`
- `category`
- `quantity`
- `price`
- `subtotal`

Formula:

- `subtotal = quantity * price`

## Order Rules

On create:

- `status = "pending"`
- `paymentStatus = "unpaid"`
- `orderType = "kiosk"`
- `orderSource = "kiosk"`

Order totals:

- order `subtotal = sum(item.subtotal)`
- order `total = subtotal`

## Firebase Calls

Read products:

```txt
GET /products.json
```

Read categories:

```txt
GET /categories.json
```

Create order:

```txt
POST /orders.json
```

Firebase create response:

```json
{
  "name": "-generatedKey"
}
```

Use returned `name` as order id.

## Main Window UI Rules

Kiosk main window should follow current style.

Use custom window chrome.

Do not use default Windows title bar.

Do not use default minimize button.

Do not use default maximize button.

Recommended window setup:

- borderless window
- maximized on launch
- warm full-screen layout
- custom exit button inside UI

Recommended behavior:

- hide standard window controls
- place exit button inside top area or side rail
- make exit button obvious for staff
- keep exit away from customer primary actions
- use one custom close action only

Exit button rules:

- label can be `Close`, `Exit`, or icon only
- button should call app close
- button should need deliberate tap
- button should not look like product action

If kiosk is customer-facing only:

- exit button may be hidden behind staff access

If kiosk is mixed-use:

- show exit button on main shell

Layout guidance from current app:

- custom left rail is acceptable
- top bar is acceptable
- rounded buttons
- warm bakery colors
- no Windows frame line
- no standard caption buttons

## Do Not Change

- do not rename fields
- do not change paths
- do not invent new product schema
- do not save kiosk orders in `/walk-in-orders`
- do not depend on stock field

# Cart Rules

Cart should follow register logic.

Do not include payment here.

Cart item shape should use:

- `productId`
- `name`
- `category`
- `quantity`
- `price`
- `subtotal`

Add to cart behavior:

- when product tapped, check if item already exists
- if item exists, increase `quantity` by 1
- if item does not exist, add new item with `quantity = 1`
- item `subtotal = quantity * price`

Cart display should show:

- item name
- quantity
- item price
- item subtotal

Cart controls should support:

- increase quantity
- decrease quantity
- remove item when quantity reaches zero
- clear cart

Cart totals:

- cart `subtotal = sum(item.subtotal)`
- cart `total = subtotal`

Cart notes:

- one order-level notes field is allowed
- save notes to order `notes`

Checkout preparation:

- cart data becomes `items` array in `/orders`
- customer details are collected after cart step
- payment is not part of cart logic


## Product Display Rules

Products should be shown as cards.

Each card should come from `/products`.

Each card should show:

- product image
- product name
- product price

Optional:

- category label

Product display behavior:

- load all products from `/products`
- sort by product name
- allow category filtering
- allow search by product name
- if category list exists, use `/categories`
- if categories missing, derive from product data
- if image missing, show fallback placeholder
- if category missing, show `Uncategorized`

Card behavior:

- tap card or add button adds item to cart
- same product tap adds quantity
- product card should not open payment flow

# Product Display Rules

Products should come from database only.

Do not hardcode product catalog.

Product source:

- read from `/products`
- read category labels from `/categories`
- if `/categories` missing, derive from product `category`

Product list rules:

- only show products with valid `name`
- use product key as `productId`
- sort by category first
- sort by product `name` inside category
- if category empty, show under `Uncategorized`
- if image empty, show placeholder

Product card should show:

- product image
- product name
- product category
- product price
- add to cart action

Optional product card can show:

- short description derived by app
- featured styling
- category badge

Category display rules:

- show category tabs, chips, or sections
- category label comes from `/categories/{categoryKey}/name`
- category order comes from `/categories/{categoryKey}/order`
- if `order` missing, place after ordered categories
- if category exists in product but not in `/categories`, still show it

Product interaction rules:

- tap on product should add item to cart
- product tap should not require payment step
- add action should update cart immediately
- cart summary should refresh immediately

Empty state rules:

- if no products, show empty menu state
- if load fails, show friendly error state
- allow reload if app supports it
