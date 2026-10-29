# Academic DOCX

Расширение Quarto для академических документов Microsoft Word. Действующий пример использует стандартный многофайловый Quarto Book, knitr, R и flextable. Содержание хранится в QMD, метаданные — в YAML, источники — в BibTeX.

Основной инструмент графиков — ggplot2, экспорт в DOCX — SVG через svglite; отдельный PNG при необходимости создаётся через ragg в 600 dpi. TikZ, PGFPlots и quarto_tikz исключены из рабочего стека. TeX-дистрибутив для сборки не нужен; математические выражения в LaTeX-разметке QMD по-прежнему преобразуются в редактируемые формулы Word.

Версия 0.1.0 — техническая основа. Проверены нативные заголовки, TOC, SEQ/REF, редактируемая математика OMML, таблицы с объединёнными ячейками и ограниченный пример встроенной библиографии Word. Полный профиль СТО и автоматический анализ вёрстки ещё не реализованы. Готовая сборка сейчас требует настольный Microsoft Word под Windows.

Общие стили основной части ЮУрГУ уже настроены в reference-docx: TNR 14, базовые абзацы и четыре уровня заголовков. Основания в СТО, различия параметров и образец — в [описании reference-docx](docs/REFERENCE_DOCX.md).

## Запуск

В подготовленном окружении из корня проекта:

```powershell
.\build-academic.cmd
.\build-academic.cmd --profile referat
```

Результаты: `academic/_book/coursework.docx` и `academic/_book/referat.docx`. Перед пересборкой закройте целевой DOCX в Word. Чтобы сохранить открытый результат, задайте другое имя: `--output coursework-new.docx`.

На новой Windows-машине нужны настольный Word, .NET 10 SDK и официальная Word PIA из Office/средств разработки. SDK и PIA используются для сборки служебного приложения; опубликованный вариант содержит свой .NET runtime.

```powershell
.\setup-academic.cmd
.\build-academic.cmd
```

Setup собирает C#-приложение в `.tools/academic-docx/`, подготавливает закреплённые Quarto, R и SVG-конвертер, затем восстанавливает R-пакеты из `academic/renv.lock`. Собственный рабочий конвейер не использует Python и PS1. Команды можно запускать из cmd или PowerShell; Execution Policy не требуется. Настройка PIA и команды разработчика — в [описании служебного приложения](tools/AcademicDocx/README.md).

Если инструменты уже установлены самостоятельно, из `academic/` используются стандартные команды:

```powershell
Rscript --vanilla tools/install.R
quarto render --to academic-docx --no-clean
```

В PATH должны быть Quarto, R и `rsvg-convert`; C#-приложение должно быть подготовлено setup-командой. Для отдельного Book задайте `ACADEMIC_DOCX_SERVICE` полным путём к `academic-docx.exe`. Проверенные версии и хэши загрузок указаны в `dependencies.lock.json`; R-пакеты закреплены отдельно в `academic/renv.lock`.

## Где работать

| Каталог | Назначение |
|---|---|
| `academic/` | Исходники работы, Book-конфигурация, расширение `_extensions/academic/`, примеры и R lock-файл |
| `tools/AcademicDocx/` | C#-приложение: окружение, Word-финализация, проверки и PDF |
| `tools/AcademicDocx.Core/` | Обработка DOCX и стилей через Open XML SDK без зависимости от Word |
| `tools/AcademicDocx.Tests/` | Проверки переносимой логики и отдельные интеграционные проверки Word |
| `docs/` | План, вопросы продукта и результаты исследований |
| `output/` | Отдельно экспортированные графики и PDF; актуальные DOCX находятся в `academic/_book/` |
| `build/` | Воспроизводимые результаты проверок, не исходники |
| `archive/` | Локальная история старых прототипов, сборок и загрузок; в работе формата не используется |
| `.tools/` | Локальные установленные инструменты; не переносить и не включать в Git |

Начните с [инструкции автору](academic/AGENT_GUIDE.md) и [описания расширения](academic/README.md). Данные примера находятся в `academic/parameters.yml`, текст — в QMD-главах из `book.chapters`, источники — в `academic/references.bib`.

Обзор проведённых экспериментов: [docs/README.md](docs/README.md). Исторические A/B/C и их команды убраны из действующего интерфейса; исходники и доказательства сохранены в [архиве](archive/README.md). AutoNormoKontrol остаётся отдельным неизменённым проектом и не является зависимостью.

## Проверки

Проверка поведения на отдельной копии документа:

```powershell
.\academic-docx.cmd check-word academic/_book/referat.docx build/academic-qa/referat-mutated.docx build/academic-qa/word-check.json
```

Ручные проверки источников через интерфейс Word описаны в [инструкции расширения](academic/README.md).

Проверка объектов получает ожидаемые источники, число формул и ссылки из входного DOCX; отсутствующие сценарии редактирования отмечаются как пропущенные. Это проверка технических механизмов, а не универсальный смысловой нормоконтроль. Настройка reference-docx доступна командой `academic-docx.cmd style-reference`; отдельный Python-редактор больше не нужен.

Проверка схемы через подготовленное приложение:

```powershell
.\academic-docx.cmd validate academic/_book/coursework.docx academic/_book/referat.docx
```

Нулевая ошибка схемы не заменяет обновление и проверку поведения в Word. Диагностический режим `ACADEMIC_SKIP_WORD=1` не создаёт готовый выпуск: промежуточный пакет имеет известные ошибки схемы. Пересборка из QMD заменяет ручные правки DOCX; обратный перенос изменений из Word пока не предусмотрен.

Экспорт PDF для проверки вёрстки:

```powershell
.\academic-docx.cmd export-pdf academic/_book/coursework.docx output/coursework.pdf build/academic-qa/pdf-export.json
```

JSON отдельно фиксирует создание PDF, закрытие документа и завершение Word. Ошибка любого шага даёт ненулевой код завершения. История выбора и результаты перехода — в [архитектурном решении](docs/SERVICE_LAYER_ARCHITECTURE.md).
