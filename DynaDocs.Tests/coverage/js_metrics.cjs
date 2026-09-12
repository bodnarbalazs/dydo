/* Source identities and official ESLint/SonarJS metrics for maintained JavaScript. */
const { createRequire } = require('node:module');
const path = require('node:path');
const loadTool = createRequire(path.join(__dirname, 'package.json'));
const { Linter } = loadTool('eslint');
const sonar = loadTool('eslint-plugin-sonarjs');

function collectFunctions(rows, suppressions, moduleEdits, tokens) {
  return {
    meta: { schema: [] },
    create(context) {
      return {
        Program(node) {
          for (const token of context.sourceCode.getTokens(node)) tokens.push({
            kind: token.type, text: context.sourceCode.getText(token), line: token.loc.start.line,
            column: token.loc.start.column, end_line: token.loc.end.line, end_column: token.loc.end.column
          });
          for (const token of context.sourceCode.getAllComments().filter(item => item.type === 'Shebang')) tokens.unshift({
            kind: token.type, text: context.sourceCode.getText(token), line: token.loc.start.line,
            column: token.loc.start.column, end_line: token.loc.end.line, end_column: token.loc.end.column
          });
          suppressions.push(...context.sourceCode.getAllComments().filter(comment => /(?:istanbul|c8|v8)\s+ignore\b/.test(comment.value)).map(comment => comment.loc.start));
          for (const statement of node.body) {
            const edit = moduleEdit(statement, context.sourceCode.text);
            if (edit) moduleEdits.push(edit);
          }
        },
        ':function'(node) {
          const parent = node.parent;
          const isMethod = parent.type === 'MethodDefinition' || (parent.type === 'Property' && parent.method);
          const declaration = isMethod ? parent : node;
          const name = node.id?.name || parent.key?.name || parent.key?.value || parent.id?.name || '<anonymous>';
          rows.push({
            id: `${name}:${declaration.loc.start.line}:${declaration.loc.start.column}`,
            line: declaration.loc.start.line, column: declaration.loc.start.column,
            end_line: declaration.loc.end.line, end_column: declaration.loc.end.column,
            body: { start: node.body.loc.start, end: node.body.loc.end },
            parameters: node.params.length, constructor: parent.kind === 'constructor',
            cognitive: 0, cc: null, start: declaration.range[0], end: declaration.range[1]
          });
        }
      };
    }
  };
}


function moduleEdit(node, source) {
  if (node.type === 'ImportDeclaration' || node.type === 'ExportAllDeclaration') return { start: node.range[0], end: node.range[1], text: '' };
  if (node.type === 'ExportDefaultDeclaration') return { start: node.range[0], end: node.range[1], text: `void (${source.slice(node.declaration.range[0], node.declaration.range[1])});` };
  if (node.type === 'ExportNamedDeclaration') return { start: node.range[0], end: node.declaration?.range[0] || node.range[1], text: '' };
  return null;
}
function methodAt(rows, message) {
  const inside = rows.filter(row => {
    const startsBefore = row.line < message.line ||
      (row.line === message.line && row.column < message.column);
    const endsAfter = row.end_line > message.line ||
      (row.end_line === message.line && row.end_column >= message.column - 1);
    return startsBefore && endsAfter;
  }).sort((left, right) => (left.end - left.start) - (right.end - right.start));
  if (!inside.length || (inside.length > 1 && inside[0].start === inside[1].start && inside[0].end === inside[1].end)) {
    throw new Error(`Missing or ambiguous JavaScript metric location ${message.line}:${message.column}`);
  }
  return inside[0];
}

function applyMetric(rows, message) {
  const row = methodAt(rows, message);
  const cognitive = message.ruleId === 'sonar/cognitive-complexity';
  const match = message.message.match(cognitive ? /Complexity from (\d+) to/ : /complexity of (\d+)/);
  if (!match) throw new Error(`Unknown pinned JavaScript metric message: ${message.message}`);
  const key = cognitive ? 'cognitive' : 'cc';
  if (key === 'cc' && row.cc !== null) throw new Error(`Duplicate cyclomatic metric: ${row.id}`);
  row[key] = Number(match[1]);
}

function analyze(source, sourceType = 'commonjs') {
  source = source.replace(/^\uFEFF/, '');
  if (!['commonjs', 'module'].includes(sourceType)) throw new Error('Unknown JavaScript source type');
  const methods = [];
  const suppressions = [];
  const moduleEdits = [];
  const tokens = [];
  const globals = Object.fromEntries(['require', 'module', 'exports', '__dirname', '__filename',
    'console', 'process', 'Buffer', 'URL', 'setTimeout', 'clearTimeout', 'setInterval',
    'clearInterval', 'fetch', 'AbortController'].map(name => [name, 'readonly']));
  const config = {
    languageOptions: { ecmaVersion: 'latest', sourceType, globals },
    plugins: { local: { rules: { collect: collectFunctions(methods, suppressions, moduleEdits, tokens) } }, sonar },
    rules: { 'local/collect': 'error', complexity: ['error', 0],
      'sonar/cognitive-complexity': ['error', 0],
      'no-unused-vars': ['error', { args: 'all', caughtErrors: 'all' }],
      'no-unused-private-class-members': 'error', 'no-nested-ternary': 'error' }
  };
  const messages = new Linter().verify(source, config, { filename: 'source.cjs', allowInlineConfig: false });
  const fatal = messages.find(message => message.fatal);
  if (fatal) throw new Error(`JavaScript parse failure: ${fatal.message}`);
  const diagnostics = [];
  for (const message of messages) {
    if (['complexity', 'sonar/cognitive-complexity'].includes(message.ruleId)) applyMetric(methods, message);
    else diagnostics.push(message);
  }
  if (methods.some(row => row.cc === null)) throw new Error('Missing JavaScript cyclomatic metric');
  return { methods, diagnostics, suppressions, moduleEdits, tokens };
}

function moduleMetrics(source, sourceType) {
  source = source.replace(/^\uFEFF/, '');
  const original = analyze(source, sourceType);
  for (const edit of original.moduleEdits.sort((left, right) => right.start - left.start)) {
    source = source.slice(0, edit.start) + edit.text + source.slice(edit.end);
  }
  source = source.replace(/^#![^\r\n]*/, '');
  const wrapped = analyze(`async function __dydo_module__(){\n${source}\n}`, sourceType);
  const entry = wrapped.methods[0];
  if (!entry || entry.line !== 1 || entry.column !== 0) throw new Error('Missing logical JavaScript module metric');
  return { cc: entry.cc, cognitive: entry.cognitive };
}

module.exports = { analyze, moduleMetrics };

if (require.main === module) {
  const fs = require('node:fs');
  process.stdout.write(JSON.stringify(analyze(fs.readFileSync(0, 'utf8'), process.argv[2])));
}
