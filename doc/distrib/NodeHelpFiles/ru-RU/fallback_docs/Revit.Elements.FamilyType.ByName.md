## Подробности
Узел `FamilyType.ByName` пытается извлечь указанный типоразмер семейства с данным именем из текущего документа. Если типоразмер семейства недоступен в текущем документе, возвращается значение null.

Примечание. Узел `FamilyType.ByName` выполняет поиск в определениях типоразмеров семейств в порядке создания родительского семейства (по идентификатору элемента). Если несколько родительских семейств имеют определение типоразмера с одинаковым именем, возвращается первое найденное. Для более точного поиска типоразмеров семейств используйте узел `FamilyType.ByFamilyAndName` или `FamilyType.ByFamilyNameAndTypeName`.

В приведенном ниже примере возвращается типоразмер семейства дверей «36" x 84"» из семейства «Дверь-Маятниковая-Одинарная-Щитовая».
___
## Файл примера

![FamilyType.ByName](./Revit.Elements.FamilyType.ByName_img.jpg)
