// Monaco support for the assembly language. Highlighting runs in the browser from the current mnemonic list; completion,
// hover and formatting call back into the C# language service (AssemblyLanguage) through a .NET object reference.
window.exuarchAsm = {
    languageId: 'exuarch-asm',
    service: null,
    registered: false,

    // Called by the editor component once Monaco is loaded, and again whenever the instruction set changes.
    register: function (service, mnemonics) {
        const monaco = window.monaco;
        this.service = service;
        const id = this.languageId;
        if (!this.registered) {
            this.registered = true;
            // Links in hovers written exuarch:guide/... open the handbook. Monaco drops links with schemes it does not
            // know, so the hover turns them into this command, which trusted hover text may run.
            const language = this;
            monaco.editor.registerCommand('exuarch.open', function (accessor, href) {
                language.service.invokeMethodAsync('OpenLink', href);
            });
            monaco.languages.register({ id: id });
            monaco.languages.setLanguageConfiguration(id, {
                comments: { lineComment: ';' },
                wordPattern: /\.?[A-Za-z_][A-Za-z0-9_]*/,
                autoClosingPairs: [{ open: '"', close: '"', notIn: ['string', 'comment'] }],
            });
            monaco.editor.defineTheme('exuarch', {
                base: 'vs',
                inherit: true,
                rules: [
                    { token: 'keyword', foreground: '17181c', fontStyle: 'bold' },
                    { token: 'keyword.directive', foreground: '0c8599', fontStyle: 'bold' },
                    { token: 'type.label', foreground: '7c5cc4', fontStyle: 'bold' },
                    { token: 'identifier.label', foreground: '7c5cc4' },
                    { token: 'number', foreground: '2f6fdb' },
                    { token: 'string', foreground: 'b8340f' },
                    { token: 'comment', foreground: '9a9ea6' },
                    { token: 'delimiter', foreground: '7d828c' },
                ],
                colors: {
                    'editor.background': '#ffffff',
                    'editor.foreground': '#17181c',
                    'editor.lineHighlightBackground': '#faf9f6',
                    'editor.lineHighlightBorder': '#00000000',
                    'editorLineNumber.foreground': '#b9bcc2',
                    'editorLineNumber.activeForeground': '#17181c',
                    'editorGutter.background': '#faf9f6',
                    'editorCursor.foreground': '#f04f23',
                    'editor.selectionBackground': '#fde6de',
                    'editor.inactiveSelectionBackground': '#f3f2ee',
                    'editorIndentGuide.background1': '#ebe9e3',
                    'editorWidget.background': '#ffffff',
                    'editorWidget.border': '#cfccc2',
                    'editorSuggestWidget.selectedBackground': '#f3f2ee',
                    'editorHoverWidget.background': '#ffffff',
                    'editorHoverWidget.border': '#cfccc2',
                },
            });
            // The same, for the dark theme (see exuarchTheme in machineEditor.js).
            monaco.editor.defineTheme('exuarch-dark', {
                base: 'vs-dark',
                inherit: true,
                rules: [
                    { token: 'keyword', foreground: 'e9e7e2', fontStyle: 'bold' },
                    { token: 'keyword.directive', foreground: '3bc2d4', fontStyle: 'bold' },
                    { token: 'type.label', foreground: 'a58fe2', fontStyle: 'bold' },
                    { token: 'identifier.label', foreground: 'a58fe2' },
                    { token: 'number', foreground: '6f9ff2' },
                    { token: 'string', foreground: 'ff9d7c' },
                    { token: 'comment', foreground: '7a7f89' },
                    { token: 'delimiter', foreground: '8b909a' },
                ],
                colors: {
                    'editor.background': '#1b1e23',
                    'editor.foreground': '#e9e7e2',
                    'editor.lineHighlightBackground': '#20242a',
                    'editor.lineHighlightBorder': '#00000000',
                    'editorLineNumber.foreground': '#5f646d',
                    'editorLineNumber.activeForeground': '#e9e7e2',
                    'editorGutter.background': '#20242a',
                    'editorCursor.foreground': '#ff6a3d',
                    'editor.selectionBackground': '#3b2119',
                    'editor.inactiveSelectionBackground': '#2b2f36',
                    'editorIndentGuide.background1': '#2b2f36',
                    'editorWidget.background': '#1b1e23',
                    'editorWidget.border': '#3b404a',
                    'editorSuggestWidget.selectedBackground': '#2b2f36',
                    'editorHoverWidget.background': '#1b1e23',
                    'editorHoverWidget.border': '#3b404a',
                },
            });

            const self = this;
            monaco.languages.registerCompletionItemProvider(id, {
                triggerCharacters: ['.', ' ', ','],
                provideCompletionItems: async function (model, position) {
                    const word = model.getWordUntilPosition(position);
                    const range = { startLineNumber: position.lineNumber, endLineNumber: position.lineNumber, startColumn: word.startColumn, endColumn: word.endColumn };
                    const items = JSON.parse(await self.service.invokeMethodAsync('Complete', model.getValue(), position.lineNumber, position.column));
                    const kinds = { 0: monaco.languages.CompletionItemKind.Function, 1: monaco.languages.CompletionItemKind.Keyword, 2: monaco.languages.CompletionItemKind.Reference };
                    return {
                        suggestions: items.map(function (item, index) {
                            return {
                                label: { label: item.label, description: item.detail },
                                kind: kinds[item.kind],
                                detail: item.detail,
                                documentation: item.documentation,
                                insertText: item.insertText,
                                range: range,
                                sortText: String(index).padStart(4, '0'),
                                command: item.insertText.endsWith(' ') ? { id: 'editor.action.triggerSuggest', title: 'operands' } : undefined,
                            };
                        }),
                    };
                },
            });
            monaco.languages.registerHoverProvider(id, {
                provideHover: async function (model, position) {
                    const markdown = await self.service.invokeMethodAsync('Hover', model.getValue(), position.lineNumber, position.column);
                    if (!markdown) return null;
                    const value = markdown.replace(/\]\((exuarch:[^)]+)\)/g, function (match, href) {
                        return '](command:exuarch.open?' + encodeURIComponent(JSON.stringify([href])) + ')';
                    });
                    return { contents: [{ value: value, isTrusted: true }] };
                },
            });
            monaco.languages.registerDocumentFormattingEditProvider(id, {
                provideDocumentFormattingEdits: async function (model) {
                    const text = await self.service.invokeMethodAsync('Format', model.getValue());
                    return [{ range: model.getFullModelRange(), text: text }];
                },
            });
            // Used by format on paste: formats with the whole document's columns, changing only lines in the range.
            monaco.languages.registerDocumentRangeFormattingEditProvider(id, {
                provideDocumentRangeFormattingEdits: async function (model, range) {
                    const formatted = (await self.service.invokeMethodAsync('Format', model.getValue())).split('\n');
                    const edits = [];
                    for (let line = range.startLineNumber; line <= Math.min(range.endLineNumber, model.getLineCount()); line++) {
                        const text = formatted[line - 1];
                        if (text !== undefined && text !== model.getLineContent(line)) {
                            edits.push({ range: { startLineNumber: line, startColumn: 1, endLineNumber: line, endColumn: model.getLineMaxColumn(line) }, text: text });
                        }
                    }
                    return edits;
                },
            });
            // Format on type: when Enter is pressed, tidy the line that was just finished.
            monaco.languages.registerOnTypeFormattingEditProvider(id, {
                autoFormatTriggerCharacters: ['\n'],
                provideOnTypeFormattingEdits: async function (model, position) {
                    const line = position.lineNumber - 1;
                    if (line < 1) return [];
                    const text = await self.service.invokeMethodAsync('FormatLine', model.getValue(), line);
                    if (text === model.getLineContent(line)) return [];
                    return [{ range: { startLineNumber: line, startColumn: 1, endLineNumber: line, endColumn: model.getLineMaxColumn(line) }, text: text }];
                },
            });
        }
        monaco.languages.setMonarchTokensProvider(id, {
            ignoreCase: true,
            mnemonics: mnemonics,
            tokenizer: {
                root: [
                    [/;.*$/, 'comment'],
                    [/[A-Za-z_][A-Za-z0-9_]*:/, 'type.label'],
                    [/\.[A-Za-z]+/, 'keyword.directive'],
                    [/#?0[xX][0-9A-Fa-f]+/, 'number'],
                    [/#?[0-9]+/, 'number'],
                    [/#?'(\\.|[^'\\])'/, 'string'],
                    [/"([^"\\]|\\.)*"/, 'string'],
                    [/"[^"]*$/, 'string'],
                    [/[A-Za-z_][A-Za-z0-9_]*/, { cases: { '@mnemonics': 'keyword', '@default': 'identifier.label' } }],
                    [/,/, 'delimiter'],
                ],
            },
        });
    },

    // Replaces the problem markers on the editor's model.
    setMarkers: function (editorId, markersJson) {
        const monaco = window.monaco;
        const markers = JSON.parse(markersJson);
        const editor = monaco.editor.getEditors().find(function (e) { return e.getContainerDomNode().id === editorId || e.getContainerDomNode().closest('#' + editorId); });
        const model = editor ? editor.getModel() : null;
        if (!model) return;
        monaco.editor.setModelMarkers(model, 'exuarch-asm', markers.map(function (m) {
            return {
                startLineNumber: m.line, endLineNumber: m.line, startColumn: m.startColumn, endColumn: m.endColumn,
                message: m.message, severity: m.warning ? monaco.MarkerSeverity.Warning : monaco.MarkerSeverity.Error,
            };
        }));
    },

    // Runs Monaco's format document action on the editor.
    format: function (editorId) {
        const editor = window.monaco.editor.getEditors().find(function (e) { return e.getContainerDomNode().closest('#' + editorId); });
        if (editor) editor.getAction('editor.action.formatDocument').run();
    },
};
