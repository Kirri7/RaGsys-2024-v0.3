using System.Text.RegularExpressions;

namespace BCG;

public static class ByteCodeGenerator
{
    public static ByteCode Generate(string text)
    {
        var result = new ByteCode();
        
        var lines = text.Split('\n');
        int previousIndentation = 0;
        int currentIndentation = 0;
        var globalCodeBlock = new CodeBlock();
        var currentCodeBlock = globalCodeBlock;
        bool mustIndent = false;
        
        globalCodeBlock.AddInstruction(new HelperInstruction(HelperInstructionType.RESUME, 0));

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            currentIndentation = GetLineIndentation(line, i);

            if ((!mustIndent && currentIndentation > previousIndentation) ||
                (mustIndent && currentIndentation - previousIndentation != 1))
            {
                throw new Exception("Invalid indentation on line " + (i + 1)); 
            }
            
            if (currentIndentation < previousIndentation)
            {
                int diff = previousIndentation - currentIndentation;
                for (int j = 0; j < diff; j++)
                {
                    currentCodeBlock = currentCodeBlock.Parent;
                }
            }

            var lineType = GetLineType(line, i);

            mustIndent = lineType is StringType.IF or StringType.ELIF or StringType.ELSE or StringType.WHILE;
            
            
            


            previousIndentation = currentIndentation;
        }
        result.ConstTable.Add("None", result.ConstTable.Count);
        globalCodeBlock.AddInstruction(new MemoryInstruction(MemoryInstructionType.LOAD_CONST, result.ConstTable.Count - 1, lines.Length));
        globalCodeBlock.AddInstruction(new HelperInstruction(HelperInstructionType.RETURN_VALUE, lines.Length));
        globalCodeBlock.MakeGoto();
        
        result.Code = globalCodeBlock.DumpToByteCode(result);
        return result;
    }
    
    enum StringType
    {
        ASSIGN,
        PRINT,
        IF,
        ELIF,
        ELSE,
        WHILE
    }

    private static int GetLineIndentation(string line, int number)
    {
        if (line[0] == ' ')
        {
            int count = line.TakeWhile(c => c == ' ').Count();
            if (count % 4 != 0)
            {
                throw new Exception("Invalid indentation on line " + number);
            }

            return count;
        } else if (line[0] == '\t')
        {
            return line.TakeWhile(c => c == '\t').Count();
        } else if (!(char.IsLetterOrDigit(line[0]) || line[0] == '('))
        {
            throw new Exception("Invalid beginning of line " + number);
        }

        return 0;
    }

    private static StringType GetLineType(string line, int number)
    {
        line = line.Trim();
        
        string assignPattern = @"^[a-zA-Z][a-zA-Z0-9_]* *= *[a-zA-Z0-9_ \(\)\+\-\*/]+$";
        string printPattern = @"^print\([a-zA-Z0-9_ \(\)\+\-\*/]+\)$";
        string ifPattern = @"^if [a-zA-Z0-9_ \(\)\+\-\*/=<>]+:$";
        string elifPattern = @"^elif [a-zA-Z0-9_ \(\)\+\-\*/=<>]+:$";
        string elsePattern = @"^else:$";
        string whilePattern = @"^while [a-zA-Z0-9_ \(\)\+\-\*/=<>]+:$";

        if (Regex.IsMatch(line, assignPattern))
            return StringType.ASSIGN;
        if (Regex.IsMatch(line, printPattern))
            return StringType.PRINT;
        if (Regex.IsMatch(line, ifPattern))
            return StringType.IF;
        if (Regex.IsMatch(line, elifPattern))
            return StringType.ELIF;
        if (Regex.IsMatch(line, elsePattern))
            return StringType.ELSE;
        if (Regex.IsMatch(line, whilePattern))
            return StringType.WHILE;

        throw new Exception("Unknown expression on line + " + number);
    }

    private static List<Instruction> ParseExpression(string expression, bool toBool, ByteCode code)
    {
        // TODO:
    }
    
    public struct ByteCode()
    {
        public string Code = "";
        public Dictionary<string, int> NameTable = new();
        public Dictionary<object, int> ConstTable = new();
    }

    private abstract class Instruction(int line)
    {
        public int Line = line;
        public int Id = 0;
        public int GoToId = 0;

        public virtual void SetId(ref int id)
        {
            Id = id;
            ++id;
        }

        public virtual int GetGoToId()
        {
            return GoToId;
        }

        public virtual void SetGoToId(int id)
        {
            GoToId = id;
        }
        
        public virtual CodeBlock? GetInheritedCodeBlock() => null;
        public virtual bool NeedMarkNext() => false;
        public virtual bool NeedMarkThis() => false;
        public virtual void UseMarks(CodeBlock code) {}
        public abstract string Dump(ref int previousInstructionLine, ByteCode code);
    }

    enum HelperInstructionType
    {
        RESUME,
        RETURN_VALUE,
        TO_BOOL,
        CALL,
        POP_TOP
    }

    private class HelperInstruction : Instruction
    {
        public HelperInstructionType InsType;

        public HelperInstruction(HelperInstructionType type, int line) : base(line)
        {
            InsType = type;
        }

        public override string Dump(ref int previousInstructionLine, ByteCode _)
        {
            string st = previousInstructionLine == Line ? "" : "\n";
            string number = previousInstructionLine == Line ? "" : Line.ToString();
            string gotoMark = GoToId == 0 ? "" : "L" + GoToId.ToString() + ":";
            string name = InsType.ToString();
            string value = InsType == HelperInstructionType.CALL ? "1" : "";

            previousInstructionLine = Line;

            return $"{st}{number, 3} {gotoMark, 5}     {name, -22} {value}";
        }
    }

    enum JumpInstructionType
    {
        POP_JUMP_IF_FALSE,
        POP_JUMP_IF_TRUE,
        JUMP_FORWARD,
        JUMP_BACKWARD
    }
    
    private class JumpInstruction : Instruction
    {
        public JumpInstructionType InsType;
        public int Value = 0;
        public int MarkId = 0;

        public JumpInstruction(JumpInstructionType type, int line) : base(line)
        {
            InsType = type;
        }

        public void SetValueAndMark(int value, int mark)
        {
            Value = value;
            MarkId = mark;
        }

        public override string Dump(ref int previousInstructionLine, ByteCode _)
        {
            string number = previousInstructionLine == Line ? "" : Line.ToString();
            string gotoMark = GoToId == 0 ? "" : "L" + GoToId.ToString() + ":";
            string name = InsType.ToString();
            string value = Value.ToString();
            string annotation = "(to L" + MarkId + ")";

            previousInstructionLine = Line;

            return $"{number, 3} {gotoMark, 5}     {name, -22} {value, 3} {annotation}";
        }
    }

    enum ContainerInstructionType
    {
        IF,
        ELIF,
        ELSE,
        WHILE
    }
    
    private class ContainerInstruction : Instruction
    {
        public ContainerInstructionType InsType;
        public CodeBlock Expression;
        public CodeBlock Inner;
        public JumpInstruction StartIns;
        public JumpInstruction EndIns;

        public ContainerInstruction(ContainerInstructionType type, string expression, CodeBlock currentBlock, int line) : base(line)
        {
            InsType = type;
            Expression = new CodeBlock();
            Inner = new CodeBlock();
            Expression.Parent = currentBlock;
            Inner.Parent = currentBlock;

            if (InsType != ContainerInstructionType.ELSE)
                Expression.Instructions = ParseExpression(expression, true);

            switch (InsType)
            {
                case ContainerInstructionType.IF:
                    StartIns = new JumpInstruction(JumpInstructionType.POP_JUMP_IF_FALSE, line);
                    EndIns = new JumpInstruction(JumpInstructionType.JUMP_FORWARD, line);
                    break;
                case ContainerInstructionType.ELIF:
                    StartIns = new JumpInstruction(JumpInstructionType.POP_JUMP_IF_FALSE, line);
                    EndIns = new JumpInstruction(JumpInstructionType.JUMP_FORWARD, line);
                    break;
                case ContainerInstructionType.ELSE:
                    StartIns = null;
                    EndIns = null;
                    break;
                case ContainerInstructionType.WHILE:
                    StartIns = new JumpInstruction(JumpInstructionType.POP_JUMP_IF_FALSE, line);
                    EndIns = new JumpInstruction(JumpInstructionType.JUMP_BACKWARD, line);
                    break;
            }
        }

        public override string Dump(ref int previousInstructionLine, ByteCode code)
        {
            var result = "";

            if (InsType != ContainerInstructionType.ELSE)
            {
                result += Expression.DumpToByteCode(code, ref previousInstructionLine);
                result += StartIns.Dump(ref previousInstructionLine, code);
            }

            result += Inner.DumpToByteCode(code, ref previousInstructionLine);

            if (InsType != ContainerInstructionType.ELSE)
            {
                result += EndIns.Dump(ref previousInstructionLine, code);
            }

            return result;
        }

        public override CodeBlock? GetInheritedCodeBlock() => Inner;

        public override bool NeedMarkNext() => InsType != ContainerInstructionType.ELSE;
        public override bool NeedMarkThis() => InsType == ContainerInstructionType.WHILE;

        public override int GetGoToId()
        {
            return InsType == ContainerInstructionType.ELSE ? 0 : Expression.Instructions[0].GetGoToId();
        }

        public override void SetGoToId(int id)
        {
            if (InsType != ContainerInstructionType.ELSE)
                Expression.Instructions[0].SetGoToId(id);
        }

        public override void SetId(ref int id)
        {
            if (InsType != ContainerInstructionType.ELSE)
            {
                Expression.SetInstructionIdsInternal(ref id);
                StartIns.SetId(ref id);
            }

            Inner.SetInstructionIdsInternal(ref id);

            if (InsType != ContainerInstructionType.ELSE)
            {
                EndIns.SetId(ref id);
            }
        }

        public override void UseMarks(CodeBlock code)
        {
            base.UseMarks(code);
        }
    }
    
    enum MemoryInstructionType
    {
        LOAD_CONST,
        LOAD_NAME,
        LOAD_SMALL_INT,
        STORE_NAME
    }
    
    private class MemoryInstruction : Instruction
    {
        public MemoryInstructionType InsType;
        public int Value;

        public MemoryInstruction(MemoryInstructionType type, int value, int line) : base(line)
        {
            InsType = type;
            Value = value;
        }

        public override string Dump(ref int previousInstructionLine, ByteCode code)
        {
            string st = previousInstructionLine == Line ? "" : "\n";
            string number = previousInstructionLine == Line ? "" : Line.ToString();
            string gotoMark = GoToId == 0 ? "" : "L" + GoToId.ToString() + ":";
            string name = InsType.ToString();
            string value = Value.ToString();
            string annotation = "";

            if (InsType == MemoryInstructionType.LOAD_SMALL_INT)
                annotation = "";
            else if (InsType == MemoryInstructionType.LOAD_CONST)
                annotation = code.ConstTable.FirstOrDefault(x => x.Value == Value).Key.ToString();
            else
                annotation = code.NameTable.FirstOrDefault(x => x.Value == Value).Key.ToString();
            

            previousInstructionLine = Line;

            var result = $"{st}{number, 3} {gotoMark, 5}     {name, -22} {value, 3}";

            if (!string.IsNullOrWhiteSpace(annotation))
                result += "(" + annotation + ")";
            return result;
        }
    }
    
    enum ArithmeticInstructionType
    {
        BINARY_ADD,
        BINARY_SUBTRACT,
        BINARY_MULTIPLY,
        BINARY_DIVIDE,
        BINARY_MODULO
    }
    
    private class ArithmeticInstruction : Instruction
    {
        public ArithmeticInstructionType InsType;

        public ArithmeticInstruction(ArithmeticInstructionType type, int line) : base(line)
        {
            InsType = type;
        }

        public override string Dump(ref int previousInstructionLine, ByteCode _)
        {
            string st = previousInstructionLine == Line ? "" : "\n";
            string number = previousInstructionLine == Line ? "" : Line.ToString();
            string gotoMark = GoToId == 0 ? "" : "L" + GoToId.ToString() + ":";
            string name = "BINARY_OP";
            string value = "";
            string annotation = "";

            previousInstructionLine = Line;

            switch (InsType)
            {
                case ArithmeticInstructionType.BINARY_ADD:
                    value = "0";
                    annotation = "+";
                    break;
                case ArithmeticInstructionType.BINARY_SUBTRACT:
                    value = "10";
                    annotation = "-";
                    break;
                case ArithmeticInstructionType.BINARY_MULTIPLY:
                    value = "5";
                    annotation = "*";
                    break;
                case ArithmeticInstructionType.BINARY_DIVIDE:
                    value = "11";
                    annotation = "/";
                    break;
                case ArithmeticInstructionType.BINARY_MODULO:
                    value = "6";
                    annotation = "%";
                    break;
            }

            return $"{st}{number, 3} {gotoMark, 5}     {name, -22} {value, 3} ({annotation})";
        }
    }
    
    enum LogicInstructionType
    {
        EQUAL,
        LESS,
        GREATER,
        LESS_EQUAL,
        GREATER_EQUAL,
        NOT_EQUAL
    }
    
    private class LogicInstruction : Instruction
    {
        public LogicInstructionType InsType;

        public LogicInstruction(LogicInstructionType type, int line) : base(line)
        {
            InsType = type;
        }

        public override string Dump(ref int previousInstructionLine, ByteCode _)
        {
            string st = previousInstructionLine == Line ? "" : "\n";
            string number = previousInstructionLine == Line ? "" : Line.ToString();
            string gotoMark = GoToId == 0 ? "" : "L" + GoToId.ToString() + ":";
            string name = "BINARY_OP";
            string value = "";
            string annotation = "";

            previousInstructionLine = Line;

            switch (InsType)
            {
                case LogicInstructionType.EQUAL:
                    value = "72";
                    annotation = "==";
                    break;
                case LogicInstructionType.LESS:
                    value = "2";
                    annotation = "<";
                    break;
                case LogicInstructionType.GREATER:
                    value = "132";
                    annotation = ">";
                    break;
                case LogicInstructionType.LESS_EQUAL:
                    value = "42";
                    annotation = "<=";
                    break;
                case LogicInstructionType.GREATER_EQUAL:
                    value = "172";
                    annotation = ">=";
                    break;
                case LogicInstructionType.NOT_EQUAL:
                    value = "103";
                    annotation = "!=";
                    break;
            }

            return $"{st}{number, 3} {gotoMark, 5}     {name, -22} {value, 3} ({annotation})";
        }
    }

    private class CodeBlock
    {
        public List<Instruction> Instructions = [];
        public CodeBlock Parent = null;

        public void SetInstructionIds()
        {
            int id = 0;
            SetInstructionIdsInternal(ref id);
        }

        public string DumpToByteCode(ByteCode code)
        {
            int line = 0;
            return DumpToByteCode(code, ref line);
        }
        
        public string DumpToByteCode(ByteCode code, ref int line)
        {
            var result = "";
            foreach (var instruction in Instructions)
            {
                result += instruction.Dump(ref line, code) + '\n';
            }

            return result;
        }

        public void AddInstruction(Instruction inst)
        {
            Instructions.Add(inst);
        }

        public void SetInstructionIdsInternal(ref int id)
        {
            foreach (var instruction in Instructions)
            {
                instruction.SetId(ref id);
            }
        }

        public void MakeGoto()
        {
            int mark = 1;
            bool needMark = false;
            SetInstructionIds();
            MarkCode(ref mark, ref needMark);
            UseMarks();
        }

        private void MarkCode(ref int mark, ref bool needMark)
        {
            for (int i = 0; i < Instructions.Count; ++i)
            {
                if ((needMark || Instructions[i].NeedMarkThis()) && Instructions[i].GetGoToId() == 0)
                {
                    Instructions[i].SetGoToId(mark);
                    ++mark;
                }
                needMark = false;

                if (Instructions[i].GetInheritedCodeBlock() is not null)
                {
                    Instructions[i].GetInheritedCodeBlock().MarkCode(ref mark, ref needMark);
                }

                if (Instructions[i].NeedMarkNext())
                    needMark = true;
            }
        }

        private void UseMarks()
        {
            foreach (var instruction in Instructions)
            {
                instruction.UseMarks(this);
                if (instruction.GetInheritedCodeBlock() is not null)
                {
                    instruction.GetInheritedCodeBlock().UseMarks();
                }
            }
        }
    }
}