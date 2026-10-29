# Standard Quarto project hook. Only missing native Word mechanisms live here.
library(xml2)
W <- "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
B <- "http://schemas.openxmlformats.org/officeDocument/2006/bibliography"
REL <- "http://schemas.openxmlformats.org/package/2006/relationships"
ns <- c(w = W, m = "http://schemas.openxmlformats.org/officeDocument/2006/math")
escape <- function(x) {
  x <- gsub("&", "&amp;", as.character(x), fixed = TRUE)
  x <- gsub("<", "&lt;", x, fixed = TRUE)
  gsub('"', "&quot;", x, fixed = TRUE)
}
node <- function(x) read_xml(paste0('<root xmlns:w="', W, '" xmlns:m="', ns[["m"]], '">', x, '</root>')) |> xml_child()
run <- function(x) paste0('<w:r><w:t xml:space="preserve">', escape(x), '</w:t></w:r>')
field <- function(code) paste0('<w:r><w:fldChar w:fldCharType="begin" w:dirty="true"/></w:r><w:r><w:instrText xml:space="preserve"> ', escape(code), ' </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r>', run("0"), '<w:r><w:fldChar w:fldCharType="end"/></w:r>')
text <- function(x) paste(xml_text(xml_find_all(x, ".//w:t", ns)), collapse = "")
attr <- function(x, name) xml_attr(x, name, ns = c(w = W))
put <- function(x, name, value) xml_set_attr(x, paste0("w:", name), value, ns = c(w = W))
outputs <- strsplit(Sys.getenv("QUARTO_PROJECT_OUTPUT_FILES"), "\n", fixed = TRUE)[[1]]
outputs <- outputs[grepl("[.]docx$", outputs, ignore.case = TRUE)]
if (!length(outputs)) stop("Academic hook did not receive a DOCX output")
sources <- jsonlite::fromJSON(".quarto/academic-sources.json", simplifyVector = FALSE)

for (output in outputs) {
  output <- normalizePath(output, mustWork = TRUE)
  folder <- tempfile("academic-docx-")
  dir.create(folder)
  unzip(output, exdir = folder)
  # An incompatible SVG converter may exit successfully but emit an empty PNG.
  media <- list.files(file.path(folder, "word/media"), full.names = TRUE)
  if (length(media) && any(file.info(media)$size == 0)) stop("Empty embedded image: check the SVG converter; this DOCX is not ready")
  doc <- read_xml(file.path(folder, "word/document.xml"))
  # Flatten only Quarto one-cell figure/table containers identified by semantic IDs.
  wrappers <- xml_find_all(doc, "//w:tbl[count(w:tr)=1 and count(w:tr/w:tc)=1][w:tr/w:tc/w:bookmarkStart[starts-with(@w:name,'tbl-') or starts-with(@w:name,'fig-')]]", ns)
  for (wrapper in wrappers) {
    content <- xml_find_all(wrapper, "./w:tr/w:tc/*[not(self::w:tcPr)]", ns)
    for (part in content) xml_add_sibling(wrapper, part, .where = "before")
    xml_remove(wrapper)
  }
  # Preserve unnumbered book headings despite numbering inherited from Word styles.
  headings <- jsonlite::fromJSON(".quarto/academic-headings.json", simplifyVector = FALSE)
  for (heading in headings) {
    starts <- xml_find_all(doc, "//w:bookmarkStart", ns)
    matching <- starts[vapply(starts, attr, "", name="w:name") == heading$id]
    if (!length(matching)) next
    p <- xml_find_first(matching[[1]], "following::w:p[1]", ns)
    if (inherits(p,"xml_missing")) next
    if (!nzchar(text(p))) { xml_remove(p); next }
    pr <- xml_find_first(p,"./w:pPr",ns)
    if (inherits(pr,"xml_missing")) pr <- xml_add_child(p,node("<w:pPr/>"),.where=0)
    # Structural headings share the first outline level but have no number
    # and are centred. Lower unnumbered headings retain their own level.
    heading_style <- xml_find_first(pr, "./w:pStyle", ns)
    if (!inherits(heading_style, "xml_missing") && identical(attr(heading_style, "w:val"), "Heading1")) {
      put(heading_style, "val", "UnnumberedHeading")
    }
    xml_remove(xml_find_all(pr,"./w:numPr",ns))
    xml_add_child(pr,node('<w:numPr><w:numId w:val="0"/></w:numPr>'))
    if (isTRUE(heading$unlisted)) xml_add_child(pr,node('<w:outlineLvl w:val="9"/>'))
  }
  bookmarks <- xml_find_all(doc, "//w:bookmarkStart", ns)
  next_id <- max(c(0, as.integer(vapply(bookmarks, attr, "", name = "w:id"))), na.rm = TRUE) + 1
  for (bookmark in bookmarks) {
    name <- attr(bookmark, "w:name")
    if (!grepl("^(sec|fig|tbl|eq|lst)-", name)) next
    normalized <- gsub("[^A-Za-z0-9_]", "_", name)
    put(bookmark, "name", normalized)
    if (!grepl("^(fig|tbl|lst)-", name)) next
    label <- if (startsWith(name, "fig-")) "Рисунок" else if (startsWith(name, "tbl-")) "Таблица" else "Листинг"
    candidates <- xml_find_all(bookmark, "following::w:p", ns)
    captions <- candidates[startsWith(vapply(candidates, text, ""), label)]
    if (!length(captions)) stop("Missing caption for ", name)
    caption <- captions[[1]]
    caption_text <- sub(paste0("^", label, "[[:space:]\u00a0]+[0-9.]+[[:space:]\u00a0]*[—–:.-]?[[:space:]]*"), "", text(caption), perl = TRUE)
    if (!nzchar(caption_text)) stop("Empty caption for ", name)

    # Check if object is inside an appendix section (matching only heading paragraphs)
    preceding_headings <- xml_find_all(bookmark, "preceding::w:p[w:pPr/w:pStyle[@w:val='UnnumberedHeading' or @w:val='Heading1'] or starts-with(w:pPr/w:pStyle/@w:val, 'Heading')]", ns)
    app_letter <- NULL
    if (length(preceding_headings)) {
      h_texts <- vapply(preceding_headings, text, "")
      app_indices <- grep("^ПРИЛОЖЕНИЕ\\s+", h_texts, ignore.case = TRUE)
      if (length(app_indices)) {
        last_app_text <- h_texts[app_indices[length(app_indices)]]
        m <- regmatches(last_app_text, regexpr("^ПРИЛОЖЕНИЕ\\s+([А-ЯA-Z0-9]+)", last_app_text, ignore.case = TRUE))
        if (length(m)) {
          app_letter <- sub("^ПРИЛОЖЕНИЕ\\s+", "", m, ignore.case = TRUE)
          app_letter <- strsplit(app_letter, "[\r\n\t ]+")[[1]][[1]]
        }
      }
    }

    old_id <- attr(bookmark, "w:id")
    xml_remove(xml_find_all(doc, paste0("//w:bookmarkEnd[@w:id='", old_id, "']"), ns))
    xml_remove(bookmark)
    xml_remove(xml_find_all(caption, "./*[not(self::w:pPr)]", ns))

    if (!is.null(app_letter) && nzchar(app_letter)) {
      base_seq <- if (startsWith(name, "fig-")) "Figure" else if (startsWith(name, "tbl-")) "Table" else "Listing"
      seq_code <- paste0("SEQ ", base_seq, "_", app_letter, " \\* ARABIC")
      xml_add_child(caption, node(run(paste0(label, " "))))
      xml_add_child(caption, node(paste0('<w:bookmarkStart w:id="', next_id, '" w:name="', normalized, '"/>')))
      xml_add_child(caption, node(run(paste0(app_letter, "."))))
      for (part in xml_children(read_xml(paste0('<root xmlns:w="', W, '">', field(seq_code), '</root>')))) xml_add_child(caption, part)
      xml_add_child(caption, node(paste0('<w:bookmarkEnd w:id="', next_id, '"/>')))
      xml_add_child(caption, node(run(paste0(" — ", caption_text))))
    } else {
      seq_code <- paste0("SEQ ", label, " \\* ARABIC")
      xml_add_child(caption, node(run(paste0(label, " "))))
      xml_add_child(caption, node(paste0('<w:bookmarkStart w:id="', next_id, '" w:name="', normalized, '"/>')))
      for (part in xml_children(read_xml(paste0('<root xmlns:w="', W, '">', field(seq_code), '</root>')))) xml_add_child(caption, part)
      xml_add_child(caption, node(paste0('<w:bookmarkEnd w:id="', next_id, '"/>')))
      xml_add_child(caption, node(run(paste0(" — ", caption_text))))
    }
    next_id <- next_id + 1
  }
  # A numbered equation stays editable; attach its field to the same paragraph.
  # Keep a figure, its introductory paragraph and caption on the same page.
  for (picture in xml_find_all(doc,"//w:p[w:r/w:drawing]",ns)) {
    previous <- xml_find_first(picture,"preceding-sibling::w:p[1]",ns)
    group <- list(picture)
    if (!inherits(previous,"xml_missing") && nzchar(text(previous))) group <- c(list(previous),group)
    for (p in group) {
      pr <- xml_find_first(p,"./w:pPr",ns)
      if (inherits(pr,"xml_missing")) pr <- xml_add_child(p,node("<w:pPr/>"),.where=0)
      if (!length(xml_find_all(pr,"./w:keepNext",ns))) xml_add_child(pr,node("<w:keepNext/>"))
    }
  }
  for (p in xml_find_all(doc,"//w:p[starts-with(string(.),'Таблица ')]",ns)) {
    pr <- xml_find_first(p,"./w:pPr",ns)
    if (inherits(pr,"xml_missing")) pr <- xml_add_child(p,node("<w:pPr/>"),.where=0)
    if (!length(xml_find_all(pr,"./w:keepNext",ns))) xml_add_child(pr,node("<w:keepNext/>"))
  }
  numbers <- xml_find_all(doc, "//w:p[w:r/w:instrText[contains(.,'SEQ Формула') or contains(.,'SEQ Formula')]]", ns)
  section <- xml_find_first(doc, "//w:sectPr[last()]", ns)
  page_size <- xml_find_first(section, "./w:pgSz", ns)
  margins <- xml_find_first(section, "./w:pgMar", ns)
  equation_width <- as.integer(attr(page_size, "w:w")) -
    as.integer(attr(margins, "w:left")) - as.integer(attr(margins, "w:right"))
  if (length(numbers) && (is.na(equation_width) || equation_width <= 0)) stop("Cannot determine equation text width")
  for (number in numbers) {
    previous <- xml_find_first(number, "preceding-sibling::w:p[1]", ns)
    if (inherits(previous, "xml_missing") || !length(xml_find_all(previous, ".//m:oMath", ns))) stop("Equation number has no preceding OMML")
    pr <- xml_find_first(previous, "./w:pPr", ns)
    if (inherits(pr, "xml_missing")) pr <- xml_add_child(previous, node("<w:pPr/>"), .where = 0)
    # Standard QMD is retained; the existing Word adapter supplies native
    # numbering and places it against the actual reference's right margin.
    xml_remove(xml_find_all(pr, "./w:pStyle|./w:tabs", ns))
    xml_add_child(pr, node('<w:pStyle w:val="Formula"/>'), .where = 0)
    # Keep only an actual explanation with the equation. An unconditional
    # keepNext would also attach a following figure and displace the equation.
    explanations <- xml_find_first(number, "following-sibling::w:p[1]/w:pPr/w:pStyle[@w:val='FormulaDefinitions']", ns)
    if (!inherits(explanations, "xml_missing")) xml_add_child(pr, node("<w:keepNext/>"))
    xml_add_child(pr, node(paste0('<w:tabs><w:tab w:val="right" w:pos="', equation_width, '"/></w:tabs>')))
    xml_add_child(previous, node('<w:r><w:tab/></w:r>'))
    for (part in xml_find_all(number, "./*[not(self::w:pPr)]", ns)) xml_add_child(previous, part)
    xml_remove(number)
  }
  # Linked citeproc citations retain source IDs; verify target validity.
  citations <- xml_find_all(doc, "//w:hyperlink[starts-with(@w:anchor,'ref-')]", ns)
  source_ids <- vapply(sources, function(s) s$id, "")
  for (citation in citations) {
    key <- sub("^ref-", "", attr(citation, "w:anchor"))
    if (!key %in% source_ids) stop("Unknown bibliography source: ", key)
  }
  bib <- xml_find_all(doc, "//w:p[w:pPr/w:pStyle[@w:val='Bibliography']]", ns)
  if (length(sources) && (!length(citations) || !length(bib))) stop("Expected linked citations and a bibliography")
  # A narrow supported source mapping; reject unsupported types rather than inventing data.
  source_xml <- character()
  tag <- function(n, v) if (is.null(v) || !length(v)) "" else paste0('<b:', n, '>', escape(v), '</b:', n, '>')
  for (source in sources) {
    type <- switch(source$type, "article-journal" = "JournalArticle", "book" = "Book", "webpage" = "InternetSite", stop("Unsupported native bibliography type: ", source$type))
    authors <- vapply(source$author, function(a) if (!is.null(a$literal)) tag("Corporate", a$literal) else paste0('<b:Person>', tag("Last", a$family), tag("First", a$given), '</b:Person>'), "")
    author_xml <- if (any(vapply(source$author, function(a) !is.null(a$literal), logical(1)))) paste(authors, collapse = "") else paste0('<b:NameList>', paste(authors, collapse = ""), '</b:NameList>')
    id <- digest::digest(source$id, algo = "md5", serialize = FALSE)
    guid <- paste0('{', substr(id,1,8), '-', substr(id,9,12), '-', substr(id,13,16), '-', substr(id,17,20), '-', substr(id,21,32), '}')
    source_xml <- c(source_xml, paste0('<b:Source>', tag("Tag", source$id), tag("SourceType", type), tag("Guid", toupper(guid)), tag("Title", source$title), '<b:Author><b:Author>', author_xml, '</b:Author></b:Author>', tag("Year", source$issued$`date-parts`[[1]][[1]]), tag("JournalName", source$`container-title`), tag("Volume", source$volume), tag("Issue", source$issue), tag("Pages", source$page), tag("StandardNumber", source$DOI), tag("URL", source$URL), '</b:Source>'))
  }
  dir.create(file.path(folder, "customXml/_rels"), recursive = TRUE, showWarnings = FALSE)
  write_xml(read_xml(paste0('<b:Sources xmlns:b="', B, '" SelectedStyle="\\APASixthEditionOfficeOnline.xsl" StyleName="APA" Version="6">', paste(source_xml,collapse=""), '</b:Sources>')), file.path(folder,"customXml/item1.xml"))
  write_xml(read_xml(paste0('<ds:datastoreItem xmlns:ds="http://schemas.openxmlformats.org/officeDocument/2006/customXml" ds:itemID="{12170D43-7531-5C70-BBAA-F5993787C4B1}"><ds:schemaRefs><ds:schemaRef ds:uri="', B, '"/></ds:schemaRefs></ds:datastoreItem>')), file.path(folder,"customXml/itemProps1.xml"))
  write_xml(read_xml(paste0('<Relationships xmlns="', REL, '"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/customXmlProps" Target="itemProps1.xml"/></Relationships>')),file.path(folder,"customXml/_rels/item1.xml.rels"))
  rels <- read_xml(file.path(folder,"word/_rels/document.xml.rels"))
  xml_add_child(rels,read_xml(paste0('<Relationship xmlns="',REL,'" Id="rIdAcademicSources" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/customXml" Target="../customXml/item1.xml"/>')))
  write_xml(rels,file.path(folder,"word/_rels/document.xml.rels"))
  types <- read_xml(file.path(folder,"[Content_Types].xml"))
  xml_add_child(types,read_xml('<Override xmlns="http://schemas.openxmlformats.org/package/2006/content-types" PartName="/customXml/itemProps1.xml" ContentType="application/vnd.openxmlformats-officedocument.customXmlProperties+xml"/>'))
  write_xml(types,file.path(folder,"[Content_Types].xml"))
  # Ensure title page page number hiding (<w:titlePg/>) in section properties
  sections <- xml_find_all(doc, "//w:sectPr", ns)
  for (sect in sections) {
    if (!length(xml_find_all(sect, "./w:titlePg", ns))) {
      xml_add_child(sect, read_xml(paste0('<w:titlePg xmlns:w="', W, '"/>')))
    }
  }
  # Structural preflight: no missing REF targets or duplicate semantic bookmarks.
  names <- vapply(xml_find_all(doc,"//w:bookmarkStart",ns),attr,"",name="w:name")
  refs <- xml_text(xml_find_all(doc,"//w:instrText[starts-with(normalize-space(.),'REF ')]",ns))
  targets <- sub("^\\s*REF\\s+(\\S+).*", "\\1", refs)
  if (anyDuplicated(names) || any(!targets %in% names)) stop("Duplicate bookmark or missing native REF target")
  write_xml(doc,file.path(folder,"word/document.xml"))
  packaged <- tempfile(fileext=".docx")
  zip::zipr(packaged,files=list.files(folder,recursive=TRUE,all.files=TRUE,no..=TRUE),root=folder,mode="mirror",include_directories=FALSE)
  if (!file.copy(packaged,output,overwrite=TRUE)) stop("Cannot replace generated DOCX")
  file.copy(output,file.path(".quarto",paste0(basename(output),".before-word.docx")),overwrite=TRUE)
  if (Sys.getenv("ACADEMIC_SKIP_WORD") != "1") {
    if (.Platform$OS.type != "windows") stop("Final DOCX currently requires the Windows C# service and desktop Word")
    service <- Sys.getenv("ACADEMIC_DOCX_SERVICE")
    if (!nzchar(service)) service <- "../.tools/academic-docx/academic-docx.exe"
    if (!file.exists(service)) stop("Academic DOCX service is missing: run setup-academic.cmd or set ACADEMIC_DOCX_SERVICE")
    report <- normalizePath(file.path(".quarto",paste0(basename(output),".finalize.json")), mustWork=FALSE)
    status <- system2(normalizePath(service), c("finalize-word", shQuote(output), "--report", shQuote(report)))
    if (status != 0) stop("C# Word finalization failed; see ",report)
    message("Academic DOCX finalized: ",basename(output))
  } else {
    warning("Diagnostic draft only: fields are not updated; the pre-Word package has known schema errors. Do not treat this file as a final document.")
    message("Academic diagnostic draft: ",basename(output))
  }
}
