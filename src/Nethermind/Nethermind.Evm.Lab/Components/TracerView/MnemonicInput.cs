// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text;
using MachineStateEvents;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Terminal.Gui;
using static Nethermind.Evm.Test.EofTestsBase;

namespace Nethermind.Evm.Lab.Components.TracerView;
internal class MnemonicInput : IComponent<(ICodeInfo Bytecode, IReleaseSpec Spec)>
{
    private class CodeSection
    {
        public CodeSection(int iCount, int oCount, int sHeight)
            => (inCount, outCount, stackMax) = (iCount, oCount, sHeight);
        public int inCount = 0;
        public int outCount = 0;
        public int stackMax = 0;
        public string Body = string.Empty;
    }
    // keep view static and swap state instead 
    private bool isCached = false;
    private Dialog? container = null;
    private CheckBox? eofModeSelection= null;
    private List<CodeSection>? sectionsField= null;
    private TabView? tabView = null;
    private (Button submit, Button cancel) buttons;
    private (Button add, Button remove) actions;
    private bool isEofMode = false;
    public event Action<byte[]> BytecodeChanged;


    public void Dispose()
    {
        container?.Dispose();
        eofModeSelection?.Dispose();
        tabView?.Dispose();
        buttons.cancel?.Dispose();
        buttons.submit?.Dispose();
        actions.add?.Dispose();
        actions.remove?.Dispose();
    }


    private bool CreateNewFunctionPage(bool isFirstRender, out TextView textView, bool select = true)
    {
        if ((!isFirstRender && !isEofMode) || sectionsField is null || tabView is null || sectionsField.Count == 23)
        {
            textView = null;
            return false;
        }

        var newCodeSection = new CodeSection(0, 0, 0);
        var container = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = Colors.Menu,
        };

        var inLabel = new Terminal.Gui.Label("Inputs Count")
        {
            Width = Dim.Percent(33),
            ColorScheme = Colors.TopLevel
        };
        var inputCountField = new NumberInputField(0)
        {
            X = Pos.X(inLabel),
            Y = Pos.Bottom(inLabel),
            Width = Dim.Width(inLabel),
            Height = Dim.Percent(5),
            ColorScheme = Colors.TopLevel
        };
        inputCountField.AddFilter((_) => !isEofMode);
        inputCountField.TextChanged += (e) =>
        {
            if (Int32.TryParse((string)inputCountField.Text, out int value)) {
                newCodeSection.inCount = value;
            } else newCodeSection.inCount = 0;
        };

        var outLabel = new Terminal.Gui.Label("Outputs Count")
        {
            X = Pos.Right(inLabel) + 1,
            Width = Dim.Percent(33),
            ColorScheme = Colors.TopLevel
        };
        var outputCountField = new NumberInputField(0)
        {
            X = Pos.X(outLabel),
            Y = Pos.Bottom(outLabel),
            Width = Dim.Width(outLabel),
            Height = Dim.Percent(5),
            ColorScheme = Colors.TopLevel
        };
        outputCountField.AddFilter((_) => !isEofMode);
        outputCountField.TextChanged += (e) =>
        {
            if (!isEofMode) return;
            if (Int32.TryParse((string)outputCountField.Text, out int value)) {
                newCodeSection.outCount = value;
            } else newCodeSection.outCount = 0;
        };

        var maxLabel = new Terminal.Gui.Label("Max Stack Height")
        {
            X = Pos.Right(outLabel) + 1,
            Width = Dim.Percent(35),
            ColorScheme = Colors.TopLevel
        };
        var stackHeightField = new NumberInputField(0)
        {
            X = Pos.X(maxLabel),
            Y = Pos.Bottom(maxLabel),
            Width = Dim.Width(maxLabel),
            Height = Dim.Percent(5),
            ColorScheme = Colors.TopLevel
        };
        stackHeightField.AddFilter((_) => !isEofMode);
        stackHeightField.TextChanged += (e) => {
            if (!isEofMode) return;
            if(Int32.TryParse((string)stackHeightField.Text, out int value)) {
                newCodeSection.stackMax = value;
            } else newCodeSection.stackMax = 0;
        };

        var inputBodyField = new Terminal.Gui.TextView
        {
            Y = Pos.Bottom(outputCountField),
            Width = Dim.Fill(),
            Height = Dim.Percent(100),
            ColorScheme = Colors.Base
        };
        inputBodyField.Initialized += (s, e) =>
        {
            newCodeSection.Body = (string)inputBodyField.Text;
        };

        inputBodyField.KeyPress += (_) =>
        {
            newCodeSection.Body = (string)inputBodyField.Text;
        };

        container.Add(
            inLabel, outLabel, maxLabel,
            inputCountField,
            outputCountField,
            stackHeightField,
            inputBodyField
        );

        var currentTab = new TabView.Tab($"{sectionsField.Count}", container);
        sectionsField.Add(newCodeSection);
        tabView.AddTab(currentTab, select);
        textView = inputBodyField;
        return true;
    }

    private void RemoveSelectedFunctionPage()
    {
        if (sectionsField is null || tabView is null || sectionsField.Count == 1)
            return;

        int indexOf = tabView.Tabs.ToList().IndexOf(tabView.SelectedTab); // ugly code veeeeeery ugly
        sectionsField.RemoveAt(indexOf);
        tabView.RemoveTab(tabView.SelectedTab);

        int idx = 0;
        foreach (var tab in tabView.Tabs)
        {
            tab.Text = (idx++).ToString();
        }
    }

    private void SubmitBytecodeChanges(bool isEofContext, IEnumerable<CodeSection> functionsBytecodes)
    {
        byte[] bytecode = Array.Empty<byte>();
        if(!isEofContext)
        {
            bytecode = BytecodeParser.Parse(sectionsField[0].Body.Trim()).ToByteArray();
        } else
        {
            var scenario = new ScenarioCase(sectionsField.Select(field => new FunctionCase(field.inCount, field.outCount, field.stackMax, BytecodeParser.Parse(field.Body.Trim()).ToByteArray())).ToArray(), Array.Empty<byte>());
            bytecode = scenario.Bytecode;
        }
        BytecodeChanged?.Invoke(bytecode);
    }


    public (View, Rectangle?) View((ICodeInfo Bytecode, IReleaseSpec Spec) state, Rectangle? rect = null)
    {
        isEofMode = state.Bytecode is EofCodeInfo;

        var frameBoundaries = new Rectangle(
                X: rect?.X ?? Pos.Center(),
                Y: rect?.Y ?? Pos.Center(),
                Width: rect?.Width ?? Dim.Percent(25),
                Height: rect?.Height ?? Dim.Percent(75)
            );

        eofModeSelection ??= new CheckBox("Is Eof Mode Enabled", state.Spec.IsEip3540Enabled)
        {
            Width = Dim.Fill(),
            Height = Dim.Percent(5),
            Checked = isEofMode
        };

        tabView ??= new TabView()
        {
            Y = Pos.Bottom(eofModeSelection),
            Width = Dim.Fill(),
            Height = Dim.Percent(95),
        };

        if (isEofMode)
        {
            var eofCodeInfo = (EofCodeInfo)state.Bytecode;
            sectionsField = new List<CodeSection>(eofCodeInfo._header.CodeSections.Length);
            for(int i = 0; i <  eofCodeInfo._header.CodeSections.Length; i++)
            {
                CreateNewFunctionPage(false, out var bodyInputFieldRef, i == 0);
                var codeSectionOffsets = eofCodeInfo._header.CodeSections[i];
                var bytecodeMnemonics = BytecodeParser.Dissassemble(true, state.Bytecode.MachineCode[codeSectionOffsets.Start..codeSectionOffsets.EndOffset])
                    .ToMultiLineString(state.Spec);
                bodyInputFieldRef.Text = bytecodeMnemonics;
            }
        } else
        {
            sectionsField = new List<CodeSection>();
            CreateNewFunctionPage(isFirstRender: true, out var bodyInputFieldRef);
            var bytecodeMnemonics = BytecodeParser.Dissassemble(false, state.Bytecode.CodeSection.Span)
                .ToMultiLineString(state.Spec);
            bodyInputFieldRef.Text = bytecodeMnemonics;
        }

        actions.add ??= new Button("Add");
        actions.remove ??= new Button("Remove");
        buttons.submit ??= new Button("Submit");
        buttons.cancel ??= new Button("Cancel");
        container ??= new Dialog("Bytecode Insertion View", 100, 7, actions.add, actions.remove, buttons.submit, buttons.cancel)
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
            ColorScheme = Colors.TopLevel
        };

        if (!isCached)
        {
            container.Add(eofModeSelection, tabView); 
            buttons.submit.Clicked += () =>
            {
                try
                {
                    if (!isEofMode && sectionsField.Count > 1)
                        throw new Exception("Cannot have more than one code section in non-Eof code");

                    SubmitBytecodeChanges(isEofMode, sectionsField);
                    Application.RequestStop();
                } catch (Exception ex)
                {
                    MainView.ShowError(ex.Message);
                }
            };

            eofModeSelection.Toggled += (e) =>
            {
                eofModeSelection.Checked = isEofMode = eofModeSelection.Checked && state.Spec.IsEip3540Enabled;
            };

            buttons.cancel.Clicked += () =>
            {
                Application.RequestStop();
            };

            actions.add.Clicked += () => CreateNewFunctionPage(isFirstRender: false, out _);

            actions.remove.Clicked += RemoveSelectedFunctionPage;
        }
        isCached = true;

        return (container, frameBoundaries);
    }

}
