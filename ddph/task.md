# Async Refactor + Loading Spinners

## Repository Layer — Remove `.GetAwaiter().GetResult()`
- [x] `ProductRepository.cs` — Remove sync wrappers, make all public methods `async Task`
- [x] `SalesRepository.cs` — Make `CheckoutSale` async
- [x] `CustomItemRepository.cs` — Make `GetCustomItems` and `AddCustomItem` async
- [x] `OrderRepository.cs` — Remove sync wrapper methods (`GetOnlineOrders`, `GetRegisterOrders`, `GetKioskSales`, `UpdateOrderStatus`, `AddCustomOrder`)
- [x] `CloudinaryImageService.cs` — Make `UploadProductImage` async

## ViewModel Layer — Convert to async + add `IsLoading`
- [x] `MainWindowViewModel.cs` — async `LoadProducts`, `Checkout`; add `IsLoading`
- [x] `InventoryViewModel.cs` — async `LoadProducts`, `AddProduct`, `EditProduct`, `DeleteProduct`; add `IsLoading`
- [x] `CustomItemsViewModel.cs` — async `LoadSamples`, `SubmitCustomOrder`; add `IsLoading`
- [x] `OnlineOrdersViewModel.cs` — already async ✅ (no changes)

## XAML — Add loading overlays
- [x] `MainWindow.xaml` — Loading overlay on register content
- [x] `Views/InventoryView.xaml` — Loading overlay
- [x] `CustomItemsWindow.xaml` — Loading overlay

## View code-behind
- [x] `AddProductWindow.xaml.cs` — async save (Cloudinary upload)
