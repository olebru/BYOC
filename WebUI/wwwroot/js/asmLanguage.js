// Monaco support for BYOC assembly. Highlighting runs in the browser from the current mnemonic list; completion,
// hover and formatting call back into the C# language service (AssemblyLanguage) through a .NET object reference.
window.byocAsm = {
    languageId: 'byoc-asm',
    service: null,
    registered: false,

    // Called by the editor component once Monaco is loaded, and again whenever the instruction set changes.
    register: function (service, mnemonics) {
        const monaco = window.monaco;
        this.service = service;
        const id = this.languageId;
        if (!this.registered) {
            this.registered = true;
            monaco.languages.register({ id: id });
            monaco.languages.setLanguageConfiguration(id, {
                comments: { lineComment: ';' },
                wordPattern: /\.?[A-Za-z_][A-Za-z0-9_]*/,
                autoClosingPairs: [{ open: '"', close: '"', notIn: ['string', 'comment'] }],
            });
            monaco.editor.defineTheme('byoc', {
                base: 'vs',
                inherit: true,
                rules: [
                    { token: 'keyword', foreground: '1b6ec2', fontStyle: 'bold' },
                    { token: 'keyword.directive', foreground: '008a8a', fontStyle: 'bold' },
                    { token: 'type.label', foreground: '7b61ff', fontStyle: 'bold' },
                    { token: 'identifier.label', foreground: '7b61ff' },
                    { token: 'identifier.unknown', foreground: 'b42318' },
                    { token: 'number', foreground: '1e7b3d' },
                    { token: 'string', foreground: 'b35900' },
                    { token: 'comment', foreground: '8a94a6', fontStyle: 'italic' },
                    { token: 'delimiter', foreground: '7a8599' },
                ],
                colors: { 'editor.lineHighlightBackground': '#f4f7fc' },
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
                    return markdown ? { contents: [{ value: markdown }] } : null;
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
        monaco.editor.setModelMarkers(model, 'byoc-asm', markers.map(function (m) {
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
