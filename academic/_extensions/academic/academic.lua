-- Use ordinary Quarto IDs; only Word fields and front matter are added here.
local text = pandoc.utils.stringify
local function esc(s) return tostring(s):gsub('&','&amp;'):gsub('<','&lt;'):gsub('>','&gt;'):gsub('"','&quot;') end
local function bookmark(id) return id:gsub('[^%w_]','_') end
local function field(code)
  return '<w:r><w:fldChar w:fldCharType="begin" w:dirty="true"/></w:r><w:r><w:instrText xml:space="preserve"> '..esc(code)..' </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>0</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r>'
end
local bid=15000
local function number(id,sequence,prefix,suffix)
  bid=bid+1
  return pandoc.RawInline('openxml','<w:r><w:t xml:space="preserve">'..esc(prefix)..'</w:t></w:r><w:bookmarkStart w:id="'..bid..'" w:name="'..bookmark(id)..'"/>'..field('SEQ '..sequence..' \\* ARABIC')..'<w:bookmarkEnd w:id="'..bid..'"/><w:r><w:t xml:space="preserve">'..esc(suffix)..'</w:t></w:r>')
end
local function pagebreak() return pandoc.RawBlock('openxml','<w:p><w:r><w:br w:type="page"/></w:r></w:p>') end
local function styled(s,style)
  return pandoc.Div({pandoc.Para(s)},{['custom-style']=style})
end
local function process(doc)
  local meta=doc.meta.academic or {}
  local front=pandoc.Blocks({})
  local function value(key) return text(meta[key] or '') end
  for _,key in ipairs({'university','department','work-type','discipline','group','supervisor','city','year'}) do
    if value(key)=='' then error('Missing academic metadata: '..key) end
  end
  front:insert(styled(value('university'),'Institution'))
  front:insert(styled(value('department'),'Institution'))
  front:insert(styled(value('work-type'),'WorkType'))
  front:insert(styled(text(doc.meta.title),'Title'))
  front:insert(styled('По дисциплине «'..value('discipline')..'»','TitleDetails'))
  local author=text(doc.meta.author)
  front:insert(styled('Выполнил: '..author..', группа '..value('group'),'StudentDetails'))
  front:insert(styled('Руководитель: '..value('supervisor'),'StudentDetails'))
  front:insert(styled(value('city')..' — '..value('year'),'TitlePlace'))
  front:insert(pagebreak())
  if meta.annotation ~= false then
    front:insert(styled('Аннотация','FrontMatterHeading'))
    front:insert(pandoc.Para(text(doc.meta.title)..'. '..value('summary')))
    front:insert(pandoc.Para({pandoc.Str('Объём работы: '),pandoc.RawInline('openxml',field('NUMPAGES')),pandoc.Str(' с.')}))
    front:insert(pagebreak())
  end
  doc.meta['academic-front-matter']=pandoc.MetaBlocks(front)
  local references=pandoc.utils.references(doc)
  local bibliography=pandoc.Pandoc({}, {references=references})
  local out=assert(io.open('.quarto/academic-sources.json','wb'))
  out:write(pandoc.write(bibliography,'csljson'));out:close()
  local headings={}
  doc:walk({Header=function(h)
    if h.classes:includes('unnumbered') then
      table.insert(headings,{id=h.identifier,unlisted=h.classes:includes('unlisted')})
    end
  end})
  out=assert(io.open('.quarto/academic-headings.json','wb'))
  out:write(quarto.json.encode(headings));out:close()

  local diagnostics = {}
  local function get_input_file()
    if quarto and quarto.doc and quarto.doc.input_file then
      return quarto.doc.input_file
    end
    if PANDOC_STATE and PANDOC_STATE.input_files and #PANDOC_STATE.input_files > 0 then
      return PANDOC_STATE.input_files[1]
    end
    return "document.qmd"
  end

  local function add_diagnostic(rule, severity, file_path, section_id, section_title, object_id, location, message, recommendation)
    table.insert(diagnostics, {
      rule = rule,
      severity = severity,
      file = file_path or get_input_file(),
      section_id = section_id,
      section_title = section_title,
      object_id = object_id,
      location = location,
      message = message,
      recommendation = recommendation
    })
  end

  local function get_object_id(block, prefix)
    if block.identifier and block.identifier:match('^' .. prefix) then
      return block.identifier
    end
    if block.attr and block.attr.identifier and block.attr.identifier:match('^' .. prefix) then
      return block.attr.identifier
    end
    local found_id = nil
    pandoc.walk_block(block, {
      Header = function(h) end,
      Div = function(d)
        if d.identifier and d.identifier:match('^' .. prefix) then found_id = d.identifier end
      end,
      Table = function(t)
        if t.identifier and t.identifier:match('^' .. prefix) then found_id = t.identifier end
      end,
      Image = function(img)
        if img.identifier and img.identifier:match('^' .. prefix) then found_id = img.identifier end
      end,
      Figure = function(fig)
        if fig.identifier and fig.identifier:match('^' .. prefix) then found_id = fig.identifier end
      end
    })
    return found_id
  end

  local function is_text_para(b)
    if b.t ~= 'Para' and b.t ~= 'Plain' then return false end
    local txt = text(b)
    if txt:match('{#eq%-[%w-]+}$') then return false end
    if #b.content == 1 and (b.content[1].t == 'Image' or b.content[1].t == 'Math') then return false end
    return txt:match('%S') ~= nil
  end

  local function check_ref(para_block, obj_id)
    if not para_block then return false end
    local found = false
    pandoc.walk_block(para_block, {
      Cite = function(c)
        for _, citation in ipairs(c.citations) do
          if citation.id == obj_id or citation.id == obj_id:gsub('^fig%-', ''):gsub('^tbl%-', '') then
            found = true
          end
        end
      end,
      Str = function(s)
        if s.text:find('@' .. obj_id, 1, true) or s.text:find(obj_id, 1, true) then
          found = true
        end
      end
    })
    return found
  end

  local input_file = get_input_file()
  local current_h1 = nil
  local current_section_title = nil
  local current_section_id = nil
  local has_text_since_h1 = false
  local last_text_para = nil
  local seen_objects = {}

  local function inspect_blocks(blocks)
    for _, b in ipairs(blocks) do
      if b.t == 'Header' then
        if b.level == 1 then
          current_h1 = b
          current_section_title = text(b)
          current_section_id = b.identifier ~= '' and b.identifier or nil
          has_text_since_h1 = false
          last_text_para = nil
        elseif b.level == 2 then
          if current_h1 and not current_h1.classes:includes('unnumbered') and not current_h1.classes:includes('unlisted')
             and not b.classes:includes('unnumbered') and not b.classes:includes('unlisted') then
            if not has_text_since_h1 then
              add_diagnostic(
                'SEC_INTRO_MISSING',
                'Warning',
                input_file,
                current_section_id,
                current_section_title,
                nil,
                'между разделом «' .. (current_section_title or '') .. '» и подразделом «' .. text(b) .. '»',
                'Раздел «' .. (current_section_title or '') .. '» не содержит вводного текста перед подразделом «' .. text(b) .. '».',
                'Добавьте краткий вводный абзац о содержании раздела перед подразделом «' .. text(b) .. '».'
              )
            end
          end
          last_text_para = nil
        end
      elseif is_text_para(b) then
        has_text_since_h1 = true
        last_text_para = b
      end

      local fig_id = get_object_id(b, 'fig-')
      if fig_id and not seen_objects[fig_id] then
        seen_objects[fig_id] = true
        if not check_ref(last_text_para, fig_id) then
          add_diagnostic(
            'FIG_REF_MISSING',
            'Warning',
            input_file,
            current_section_id,
            current_section_title,
            fig_id,
            'объект #' .. fig_id,
            'Перед рисунком #' .. fig_id .. ' в предшествующем абзаце отсутствует ссылка @' .. fig_id .. '.',
            'Добавьте во вступительный абзац перед рисунком текстовую ссылку на рисунок (@' .. fig_id .. ').'
          )
        end
      end

      local tbl_id = get_object_id(b, 'tbl-')
      if tbl_id and not seen_objects[tbl_id] then
        seen_objects[tbl_id] = true
        if not check_ref(last_text_para, tbl_id) then
          add_diagnostic(
            'TBL_REF_MISSING',
            'Warning',
            input_file,
            current_section_id,
            current_section_title,
            tbl_id,
            'объект #' .. tbl_id,
            'Перед таблицей #' .. tbl_id .. ' в предшествующем абзаце отсутствует ссылка @' .. tbl_id .. '.',
            'Добавьте во вступительный абзац перед таблицей текстовую ссылку на таблицу (@' .. tbl_id .. ').'
          )
        end
      end

      if (b.t == 'Div' or b.t == 'BlockQuote') and b.content then
        inspect_blocks(b.content)
      end
    end
  end

  inspect_blocks(doc.blocks)

  out = assert(io.open('.quarto/academic-diagnostics.json', 'wb'))
  out:write(quarto.json.encode(diagnostics)); out:close()

  return doc
end
local function para(p)
  local id=text(p):match('{#(eq%-[%w-]+)}$')
  if id then
    for _,inline in ipairs(p.content) do
      if inline.t=='Math' then
        return {pandoc.Para({inline}),pandoc.Para({number(id,'Формула','(',')')})}
      end
    end
  end
end
local function cite(c)
  local is_crossref = false
  for _, citation in ipairs(c.citations) do
    local id = citation.id
    if id:match('^fig%-') or id:match('^tbl%-') or id:match('^eq%-') or id:match('^sec%-') then
      is_crossref = true
      break
    end
  end

  if is_crossref then
    if #c.citations > 1 then
      error('Grouped cross-references (fig, tbl, eq, sec) are not yet supported; use individual cross-references.')
    end
    local id = c.citations[1].id
    local reference = field('REF ' .. bookmark(id) .. (id:match('^sec%-') and ' \\n' or '') .. ' \\h')
    if id:match('^eq%-') then
      reference = '<w:r><w:t>(</w:t></w:r>' .. reference .. '<w:r><w:t>)</w:t></w:r>'
    end
    return pandoc.RawInline('openxml', reference)
  end
  return nil
end
return {{Pandoc=process},{Para=para},{Cite=cite}}
