-- Use ordinary Quarto IDs; only Word fields and front matter are added here.
local stringify = pandoc.utils.stringify
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

local function find_dash_in_string(s)
  local dashes = {"—", "–", "-"}
  local min_pos = nil
  local min_len = 1
  for _, d in ipairs(dashes) do
    local pos = s:find(d, 1, true)
    if pos then
      if not min_pos or pos < min_pos then
        min_pos = pos
        min_len = #d
      end
    end
  end
  if min_pos then
    return min_pos, min_pos + min_len - 1
  end
  return nil, nil
end

local function starts_with_where(s)
  local trimmed = s:gsub('^%s+', '')
  local head6 = trimmed:sub(1, 6)
  return head6 == 'где' or head6 == 'Где' or head6 == 'ГДЕ'
end

local function split_line_at_dash(inlines)
  local sym = {}
  local exp = {}
  local found_dash = false

  for i, inline in ipairs(inlines) do
    if not found_dash and inline.t == 'Str' then
      local s = inline.text
      local dash_pos, dash_end = find_dash_in_string(s)
      if dash_pos then
        found_dash = true
        local left = s:sub(1, dash_pos - 1)
        local right = s:sub(dash_end + 1)
        if #left > 0 then
          table.insert(sym, pandoc.Str(left))
        end
        if #right > 0 then
          table.insert(exp, pandoc.Str(right))
        end
        for j = i + 1, #inlines do
          table.insert(exp, inlines[j])
        end
        break
      else
        table.insert(sym, inline)
      end
    else
      if not found_dash then
        table.insert(sym, inline)
      else
        table.insert(exp, inline)
      end
    end
  end

  return sym, exp, found_dash
end

local function clean_sym_inlines(sym, is_first_row)
  local res = {}
  local stripped_where = false

  for _, inline in ipairs(sym) do
    if not stripped_where and inline.t == 'Str' then
      local s = inline.text
      if starts_with_where(s) then
        stripped_where = true
        local trimmed = s:gsub('^%s+', '')
        local rest = trimmed:sub(7)
        while #rest > 0 do
          local fc = rest:sub(1, 1)
          if fc == ':' or fc == ' ' or fc == '\t' then
            rest = rest:sub(2)
          else
            break
          end
        end
        local dp, de = find_dash_in_string(rest)
        if dp == 1 then
          rest = rest:sub(de + 1)
        end
        if #rest > 0 then
          table.insert(res, pandoc.Str(rest))
        end
      else
        table.insert(res, inline)
      end
    elseif not stripped_where and inline.t == 'Space' then
      -- skip leading spaces
    else
      table.insert(res, inline)
    end
  end

  while #res > 0 and res[1].t == 'Space' do
    table.remove(res, 1)
  end

  if is_first_row then
    table.insert(res, 1, pandoc.Space())
    table.insert(res, 1, pandoc.Str("где"))
  end

  while #res > 0 and res[#res].t == 'Space' do
    table.remove(res)
  end

  return res
end

local function clean_exp_inlines(exp, is_last_row)
  local res = {}
  local stripped_dash = false

  for _, inline in ipairs(exp) do
    if not stripped_dash and inline.t == 'Str' then
      local s = inline.text
      while #s > 0 do
        local stripped = false
        local fc = s:sub(1, 1)
        if fc == ' ' or fc == '\t' or fc == ':' then
          s = s:sub(2)
          stripped = true
        else
          local dp, de = find_dash_in_string(s)
          if dp == 1 then
            s = s:sub(de + 1)
            stripped = true
          end
        end
        if not stripped then break end
      end
      if #s > 0 then
        stripped_dash = true
        table.insert(res, pandoc.Str(s))
      end
    elseif not stripped_dash and inline.t == 'Space' then
      -- skip leading spaces
    else
      stripped_dash = true
      table.insert(res, inline)
    end
  end

  while #res > 0 and res[1].t == 'Space' do
    table.remove(res, 1)
  end

  while #res > 0 and res[#res].t == 'Space' do
    table.remove(res)
  end

  if #res > 0 then
    local last_idx = #res
    if res[last_idx].t == 'Str' then
      local txt = res[last_idx].text:gsub('%s+$', '')
      if is_last_row then
        txt = txt:gsub('[;,]$', '')
        if not txt:match('%.%s*$') then
          txt = txt .. '.'
        end
      else
        txt = txt:gsub('[.,]$', '')
        if not txt:match(';%s*$') then
          txt = txt .. ';'
        end
      end
      res[last_idx] = pandoc.Str(txt)
    else
      if is_last_row then
        table.insert(res, pandoc.Str("."))
      else
        table.insert(res, pandoc.Str(";"))
      end
    end
  else
    if is_last_row then
      table.insert(res, pandoc.Str("."))
    else
      table.insert(res, pandoc.Str(";"))
    end
  end

  table.insert(res, 1, pandoc.Space())
  table.insert(res, 1, pandoc.Str("—"))

  return res
end

local function math_to_openxml_runs(code)
  local xml = {}
  local i = 1
  while i <= #code do
    local c = code:sub(i, i)
    if c == '_' then
      i = i + 1
      local sub_txt = ''
      if code:sub(i, i) == '{' then
        i = i + 1
        local close_pos = code:find('}', i, true)
        if close_pos then
          sub_txt = code:sub(i, close_pos - 1)
          i = close_pos + 1
        end
      else
        sub_txt = code:sub(i, i)
        i = i + 1
      end
      table.insert(xml, '<w:r><w:rPr><w:vertAlign w:val="subscript"/></w:rPr><w:t xml:space="preserve">' .. esc(sub_txt) .. '</w:t></w:r>')
    elseif c == '^' then
      i = i + 1
      local super_txt = ''
      if code:sub(i, i) == '{' then
        i = i + 1
        local close_pos = code:find('}', i, true)
        if close_pos then
          super_txt = code:sub(i, close_pos - 1)
          i = close_pos + 1
        end
      else
        super_txt = code:sub(i, i)
        i = i + 1
      end
      table.insert(xml, '<w:r><w:rPr><w:vertAlign w:val="superscript"/></w:rPr><w:t xml:space="preserve">' .. esc(super_txt) .. '</w:t></w:r>')
    elseif c:match('[a-zA-Z]') then
      table.insert(xml, '<w:r><w:rPr><w:i/></w:rPr><w:t xml:space="preserve">' .. esc(c) .. '</w:t></w:r>')
      i = i + 1
    else
      table.insert(xml, '<w:r><w:t xml:space="preserve">' .. esc(c) .. '</w:t></w:r>')
      i = i + 1
    end
  end
  return table.concat(xml, '')
end

local function inlines_to_openxml_runs(inlines)
  local xml = {}
  for _, inline in ipairs(inlines) do
    if inline.t == 'Str' then
      table.insert(xml, '<w:r><w:t xml:space="preserve">' .. esc(inline.text) .. '</w:t></w:r>')
    elseif inline.t == 'Space' then
      table.insert(xml, '<w:r><w:t xml:space="preserve"> </w:t></w:r>')
    elseif inline.t == 'SoftBreak' or inline.t == 'LineBreak' then
      table.insert(xml, '<w:r><w:br/></w:r>')
    elseif inline.t == 'Math' then
      table.insert(xml, math_to_openxml_runs(inline.text))
    elseif inline.t == 'Emph' then
      local inner = inlines_to_openxml_runs(inline.content)
      inner = inner:gsub('<w:rPr>', '<w:rPr><w:i/>')
      inner = inner:gsub('<w:r>', '<w:r><w:rPr><w:i/></w:rPr>')
      table.insert(xml, inner)
    elseif inline.t == 'Strong' then
      local inner = inlines_to_openxml_runs(inline.content)
      inner = inner:gsub('<w:rPr>', '<w:rPr><w:b/>')
      inner = inner:gsub('<w:r>', '<w:r><w:rPr><w:b/></w:rPr>')
      table.insert(xml, inner)
    elseif inline.t == 'RawInline' and inline.format == 'openxml' then
      table.insert(xml, inline.text)
    else
      table.insert(xml, '<w:r><w:t xml:space="preserve">' .. esc(stringify(inline)) .. '</w:t></w:r>')
    end
  end
  return table.concat(xml, '')
end

local function rows_to_openxml_table(rows)
  if #rows == 0 then return nil end

  local col1_width = 1701
  local col2_width = 7512
  local tbl_indent = 709

  local tr_xmls = {}

  for i, r in ipairs(rows) do
    local is_first = (i == 1)
    local is_last = (i == #rows)

    local sym_inlines = clean_sym_inlines(r.sym, is_first)
    local exp_inlines = clean_exp_inlines(r.exp, is_last)

    local sym_runs = inlines_to_openxml_runs(sym_inlines)
    local exp_runs = inlines_to_openxml_runs(exp_inlines)

    local pPr = '<w:pPr><w:pStyle w:val="FormulaDefinitions"/><w:spacing w:before="0" w:after="0" w:line="240" w:lineRule="auto"/><w:ind w:left="0" w:firstLine="0"/></w:pPr>'

    local cell1 = '<w:tc><w:tcPr><w:tcW w:w="' .. col1_width .. '" w:type="dxa"/></w:tcPr><w:p>' .. pPr .. sym_runs .. '</w:p></w:tc>'
    local cell2 = '<w:tc><w:tcPr><w:tcW w:w="' .. col2_width .. '" w:type="dxa"/></w:tcPr><w:p>' .. pPr .. exp_runs .. '</w:p></w:tc>'

    local tr = '<w:tr><w:trPr><w:cantSplit/></w:trPr>' .. cell1 .. cell2 .. '</w:tr>'
    table.insert(tr_xmls, tr)
  end

  local tbl_xml = '<w:tbl>' ..
    '<w:tblPr>' ..
      '<w:tblStyle w:val="TableGrid"/>' ..
      '<w:tblW w:w="0" w:type="auto"/>' ..
      '<w:tblInd w:w="' .. tbl_indent .. '" w:type="dxa"/>' ..
      '<w:tblBorders>' ..
        '<w:top w:val="none" w:sz="0" w:space="0" w:color="auto"/>' ..
        '<w:left w:val="none" w:sz="0" w:space="0" w:color="auto"/>' ..
        '<w:bottom w:val="none" w:sz="0" w:space="0" w:color="auto"/>' ..
        '<w:right w:val="none" w:sz="0" w:space="0" w:color="auto"/>' ..
        '<w:insideH w:val="none" w:sz="0" w:space="0" w:color="auto"/>' ..
        '<w:insideV w:val="none" w:sz="0" w:space="0" w:color="auto"/>' ..
      '</w:tblBorders>' ..
      '<w:tblLook w:val="04A0"/>' ..
    '</w:tblPr>' ..
    '<w:tblGrid>' ..
      '<w:gridCol w:w="' .. col1_width .. '"/>' ..
      '<w:gridCol w:w="' .. col2_width .. '"/>' ..
    '</w:tblGrid>' ..
    table.concat(tr_xmls, '') ..
  '</w:tbl>'

  return pandoc.RawBlock('openxml', tbl_xml)
end

local function extract_rows_from_div(div)
  local rows = {}
  for _, child in ipairs(div.content) do
    if child.t == 'LineBlock' then
      for _, line_inlines in ipairs(child.content) do
        local sym, exp = split_line_at_dash(line_inlines)
        table.insert(rows, {sym = sym, exp = exp})
      end
    elseif child.t == 'Para' or child.t == 'Plain' then
      local sym, exp = split_line_at_dash(child.content)
      table.insert(rows, {sym = sym, exp = exp})
    elseif child.t == 'DefinitionList' then
      for _, item in ipairs(child.content) do
        local term = item[1]
        local def_inlines = {}
        if #item[2] > 0 and #item[2][1] > 0 and item[2][1][1].content then
          def_inlines = item[2][1][1].content
        end
        table.insert(rows, {sym = term, exp = def_inlines})
      end
    elseif child.t == 'BulletList' or child.t == 'OrderedList' then
      for _, item_blocks in ipairs(child.content) do
        if #item_blocks > 0 and (item_blocks[1].t == 'Para' or item_blocks[1].t == 'Plain') then
          local sym, exp = split_line_at_dash(item_blocks[1].content)
          table.insert(rows, {sym = sym, exp = exp})
        end
      end
    end
  end
  return rows
end

local function extract_rows_from_lineblock(lb)
  local rows = {}
  for _, line_inlines in ipairs(lb.content) do
    local sym, exp = split_line_at_dash(line_inlines)
    table.insert(rows, {sym = sym, exp = exp})
  end
  return rows
end

local function is_explication_lineblock(lb)
  if #lb.content == 0 then return false end
  local first_txt = stringify(pandoc.Para(lb.content[1]))
  return starts_with_where(first_txt)
end

local function extract_rows_from_deflist(dl)
  local rows = {}
  for _, item in ipairs(dl.content) do
    local term = item[1]
    local def_inlines = {}
    if #item[2] > 0 and #item[2][1] > 0 and item[2][1][1].content then
      def_inlines = item[2][1][1].content
    end
    table.insert(rows, {sym = term, exp = def_inlines})
  end
  return rows
end

local function is_explication_deflist(dl)
  if #dl.content == 0 then return false end
  local first_term_txt = stringify(pandoc.Para(dl.content[1][1]))
  return starts_with_where(first_term_txt)
end

local function extract_rows_from_list(list_block)
  local rows = {}
  for _, item_blocks in ipairs(list_block.content) do
    if #item_blocks > 0 and (item_blocks[1].t == 'Para' or item_blocks[1].t == 'Plain') then
      local sym, exp = split_line_at_dash(item_blocks[1].content)
      table.insert(rows, {sym = sym, exp = exp})
    end
  end
  return rows
end

local function transform_explications(blocks)
  local new_blocks = pandoc.Blocks({})
  local i = 1
  while i <= #blocks do
    local b = blocks[i]
    local replaced = false

    if b.t == 'Div' and b.attr and b.attr.attributes['custom-style'] == 'Formula Definitions' then
      local rows = extract_rows_from_div(b)
      if #rows > 0 then
        local raw = rows_to_openxml_table(rows)
        if raw then
          new_blocks:insert(raw)
          replaced = true
        end
      end
    elseif b.t == 'LineBlock' and is_explication_lineblock(b) then
      local rows = extract_rows_from_lineblock(b)
      if #rows > 0 then
        local raw = rows_to_openxml_table(rows)
        if raw then
          new_blocks:insert(raw)
          replaced = true
        end
      end
    elseif b.t == 'DefinitionList' and is_explication_deflist(b) then
      local rows = extract_rows_from_deflist(b)
      if #rows > 0 then
        local raw = rows_to_openxml_table(rows)
        if raw then
          new_blocks:insert(raw)
          replaced = true
        end
      end
    elseif b.t == 'Para' or b.t == 'Plain' then
      local txt = stringify(b)
      if starts_with_where(txt) then
        local trimmed = txt:gsub('^%s+', '')
        local is_standalone_where = (trimmed:match('^[гГ]де[:%s]*$') ~= nil)

        local para_rows = {}
        local consumed_count = 1

        if is_standalone_where and (i + 1) <= #blocks then
          local next_b = blocks[i + 1]
          if next_b.t == 'DefinitionList' then
            para_rows = extract_rows_from_deflist(next_b)
            consumed_count = 2
          elseif next_b.t == 'LineBlock' then
            para_rows = extract_rows_from_lineblock(next_b)
            consumed_count = 2
          elseif next_b.t == 'BulletList' or next_b.t == 'OrderedList' then
            para_rows = extract_rows_from_list(next_b)
            consumed_count = 2
          end
        end

        if #para_rows == 0 then
          local j = i
          while j <= #blocks do
            local cur = blocks[j]
            if cur.t == 'Para' or cur.t == 'Plain' then
              local cur_txt = stringify(cur)
              local dp, de = find_dash_in_string(cur_txt)
              if j == i or dp ~= nil or starts_with_where(cur_txt) then
                local sym, exp = split_line_at_dash(cur.content)
                table.insert(para_rows, {sym = sym, exp = exp})
                j = j + 1
              else
                break
              end
            else
              break
            end
          end
          consumed_count = j - i
        end

        if #para_rows > 0 then
          local raw = rows_to_openxml_table(para_rows)
          if raw then
            new_blocks:insert(raw)
            replaced = true
            i = i + consumed_count - 1
          end
        end
      end
    end

    if not replaced then
      if (b.t == 'Div' or b.attr and b.t == 'BlockQuote') and b.content then
        b.content = transform_explications(b.content)
      end
      new_blocks:insert(b)
    end

    i = i + 1
  end
  return new_blocks
end

local function process(doc)
  local meta=doc.meta.academic or {}
  local front=pandoc.Blocks({})
  local function value(key)
    if meta[key] then return stringify(meta[key]) end
    if doc.meta[key] then return stringify(doc.meta[key]) end
    return ''
  end

  local profile = value('profile')
  if profile == '' then profile = 'susu-coursework-v1' end

  if profile == 'susu-coursework-v1' or profile == 'default' then
    local ministry = value('ministry')
    if ministry == '' then ministry = 'Министерство науки и высшего образования Российской Федерации' end

    local university = value('university')
    if university == '' then university = 'ФГАОУ ВО «ЮУрГУ (НИУ)»' end

    local department = value('department')
    local work_type = value('work-type')
    if work_type == '' then work_type = 'КУРСОВАЯ РАБОТА' end

    local discipline = value('discipline')
    local group = value('group')
    local supervisor = value('supervisor')
    local city = value('city')
    local year = value('year')

    for key, val in pairs({department=department, discipline=discipline, group=group, supervisor=supervisor, city=city, year=year}) do
      if val == '' then error('Missing academic metadata: ' .. key) end
    end

    front:insert(styled(ministry, 'Institution'))
    front:insert(styled(university, 'Institution'))

    local institute = value('institute')
    if institute ~= '' then
      front:insert(styled(institute, 'Institution'))
    end

    front:insert(styled(department, 'Institution'))
    front:insert(styled(work_type, 'WorkType'))

    local title = stringify(doc.meta.title)
    front:insert(styled(title, 'Title'))
    front:insert(styled('по дисциплине «' .. discipline .. '»', 'TitleDetails'))

    local author = stringify(doc.meta.author)
    front:insert(styled('Выполнил: студент группы ' .. group .. ' ' .. author, 'StudentDetails'))
    front:insert(styled('Руководитель: ' .. supervisor, 'StudentDetails'))

    local normocontroler = value('normocontroler')
    if normocontroler ~= '' then
      front:insert(styled('Нормоконтролер: ' .. normocontroler, 'StudentDetails'))
    end

    front:insert(styled(city .. ' — ' .. year, 'TitlePlace'))
    front:insert(pagebreak())
  end

  if meta.annotation ~= false and value('summary') ~= '' then
    front:insert(styled('Аннотация', 'FrontMatterHeading'))
    front:insert(pandoc.Para(stringify(doc.meta.title) .. '. ' .. value('summary')))
    front:insert(pandoc.Para({pandoc.Str('Объём работы: '), pandoc.RawInline('openxml', field('NUMPAGES')), pandoc.Str(' с.')}))
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
    local txt = stringify(b)
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
          current_section_title = stringify(b)
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
                'между разделом «' .. (current_section_title or '') .. '» и подразделом «' .. stringify(b) .. '»',
                'Раздел «' .. (current_section_title or '') .. '» не содержит вводного текста перед подразделом «' .. stringify(b) .. '».',
                'Добавьте краткий вводный абзац о содержании раздела перед подразделом «' .. stringify(b) .. '».'
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

      local lst_id = get_object_id(b, 'lst-')
      if lst_id and not seen_objects[lst_id] then
        seen_objects[lst_id] = true
        if not check_ref(last_text_para, lst_id) then
          add_diagnostic(
            'LST_REF_MISSING',
            'Warning',
            input_file,
            current_section_id,
            current_section_title,
            lst_id,
            'объект #' .. lst_id,
            'Перед листингом #' .. lst_id .. ' в предшествующем абзаце отсутствует ссылка @' .. lst_id .. '.',
            'Добавьте во вступительный абзац перед листингом текстовую ссылку на листинг (@' .. lst_id .. ').'
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

  doc.blocks = transform_explications(doc.blocks)

  return doc
end
local function para(p)
  local id=stringify(p):match('{#(eq%-[%w-]+)}$')
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
