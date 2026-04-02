# Project Summary

## Completed Work

This POS system project now includes both:

- an admin inventory / product management module
- a main cashier screen with dynamic products, cart handling, and checkout flow

## Core Structure Built

- Added a reusable `RelayCommand` for commands
- Created a `Product` model with:
  - `Id`
  - `ProductName`
  - `Price`
  - `Stock`
  - `Category`
  - `CategoryId`
- Created a `CartItem` model with:
  - `ProductId`
  - `Item`
  - `Qty`
  - `Price`

## Inventory / Product Management Module

- Built `InventoryViewModel` using MVVM
- Built `InventoryView` for product administration
- Connected the `Products` window to host the inventory view
- Connected the inventory screen to SQL Server through `ProductRepository`

### Inventory Features Finished

- load products from the database
- search products by product name
- add products
- edit products using a form window
- delete products with confirmation
- keep the product grid read-only so editing only happens through the edit button

## Add Product / Edit Product Window

- Created `AddProductWindow`
- Used the same window for both add and edit flows
- Included input fields for:
  - Product Name
  - Price
  - Stock
  - Category
- Added validation for:
  - required product name
  - valid price
  - valid stock
- Adjusted the window so it is scrollable
- Kept this window as a normal-sized dialog instead of maximized

## Main POS Screen

- Connected `MainWindow` to `MainWindowViewModel`
- Replaced static sample product buttons with a dynamic product grid
- Loaded all product buttons from the database
- Showed one button per product
- Displayed:
  - product name
  - product price
- Added product count display
- Added a refresh button to reload products from the database

## Cart Features Finished

- Clicking a product button adds that item to the cart
- Clicking the same product again increases quantity
- Added cart grid binding on the main screen
- Added dynamic total calculation
- Added `Clear` button logic
- Added `-` button in the cart to decrease quantity
- Automatically removes an item when its quantity reaches zero

## Checkout Flow Finished

- Wired the `Checkout` button
- Implemented checkout through a SQL transaction
- Checkout now does all of the following:
  - insert into `Sales`
  - insert each cart row into `SaleItems`
  - reduce stock in `Products`
  - clear the cart after success
  - reload products so updated stock appears on screen

## Payment Handling Finished

- Added a payment textbox under the total on the main screen
- Bound the payment value to the viewmodel
- Added validation so checkout will not proceed if:
  - payment is empty
  - payment is invalid
  - payment is less than the total
- If payment is missing or invalid, checkout focuses the payment textbox
- Checkout currently saves:
  - `CashierName = "Staff"`
  - `Payment = entered payment`
  - `ChangeAmount = payment - total`

## Database Integration

The app is connected to SQL Server database `ddph`.

### Repository Classes Added

- `ProductRepository`
- `SalesRepository`
- `DbConnection`

### Current Database Operations Implemented

- load products from `Products` and `Categories`
- insert product
- update product
- delete product
- insert sale
- insert sale items
- reduce stock after checkout

## SQL Script Added

- Added `CART_TABLES.sql`
- This script creates:
  - `Sales`
  - `SaleItems`

## Window Behavior

- `MainWindow` opens maximized
- `Products` window opens maximized
- `AddProductWindow` does not open maximized

## UI Adjustments Completed

- removed barcode from the app UI and app logic
- removed barcode from product add and update flow
- inventory grid is read-only
- product buttons in main screen are generated dynamically from the database
- refresh button added beside product count
- payment textbox added under total

## Notes

- The SQL table may still contain a `Barcode` column, but the app no longer uses it
- Build is currently passing successfully
- There is still an unrelated nullable warning in `CartItem.cs`

## Main Files Involved

- `ddph/ddph/Models/Product.cs`
- `ddph/ddph/CartItem.cs`
- `ddph/ddph/RelayCommand.cs`
- `ddph/ddph/ViewModels/InventoryViewModel.cs`
- `ddph/ddph/ViewModels/MainWindowViewModel.cs`
- `ddph/ddph/Views/InventoryView.xaml`
- `ddph/ddph/Views/InventoryView.xaml.cs`
- `ddph/ddph/Views/AddProductWindow.xaml`
- `ddph/ddph/Views/AddProductWindow.xaml.cs`
- `ddph/ddph/Data/ProductRepository.cs`
- `ddph/ddph/Data/SalesRepository.cs`
- `ddph/ddph/DbConnection.cs`
- `ddph/ddph/Products.xaml`
- `ddph/ddph/Products.xaml.cs`
- `ddph/ddph/MainWindow.xaml`
- `ddph/ddph/MainWindow.xaml.cs`
- `CART_TABLES.sql`
