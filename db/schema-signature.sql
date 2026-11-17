SET NOCOUNT ON;
SELECT 'COL' k, t.name tbl, c.name col, ty.name typ, c.max_length len, c.precision p, c.scale s, c.is_nullable n, c.is_identity id
FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE t.name NOT LIKE '[_][_]%' ORDER BY t.name, c.name;
SELECT 'FK' k, name, OBJECT_NAME(parent_object_id) tbl, delete_referential_action_desc del FROM sys.foreign_keys ORDER BY name;
SELECT 'IX' k, i.name, OBJECT_NAME(i.object_id) tbl, i.type_desc, i.is_unique FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id WHERE t.name NOT LIKE '[_][_]%' AND i.name IS NOT NULL ORDER BY i.name;
SELECT 'SEQ' k, name, start_value, increment, TYPE_NAME(user_type_id) typ FROM sys.sequences ORDER BY name;
