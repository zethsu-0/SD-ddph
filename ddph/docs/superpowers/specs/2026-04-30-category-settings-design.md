# Category Settings Design

## Assumptions

- Categories are stored in Firebase under `categories`.
- Category names are the user-facing values saved on products.
- Renaming a category updates all products using the old name.
- Deleting a category is blocked while products still use it.
- `Uncategorized` stays available and is not deleted.

## Success Criteria

- Settings tab can add categories.
- Settings tab can edit category names.
- Changes save to Firebase.
- Renamed categories update existing products.
- Product and register category lists reflect saved changes after refresh.
- Tests cover add, rename, and blocked delete behavior.

## Approach

Add a dedicated `CategoryRepository`.
It owns Firebase category reads and writes.
It also updates product category values during rename.
This keeps category logic out of settings UI code.

## UI

Add a category card in `CreateSettingsContent`.
The card contains:

- Category name input.
- Add or save button.
- Category list.
- Edit and remove buttons.
- Status text.

The card follows existing settings controls.
No new screen is needed.

## Data Flow

Loading settings fetches categories.
Adding writes a normalized category record.
Editing writes the new category record, removes the old category key, and patches matching products.
Deleting removes only unused categories.

## Validation

Blank names are rejected.
Duplicate names are rejected case-insensitively.
`Uncategorized` cannot be removed.
Categories used by products cannot be removed.

## Testing

Add repository tests with a fake Firebase client.
Tests verify:

- Add writes a category record.
- Rename moves category data.
- Rename updates matching products.
- Delete is blocked when products use the category.
