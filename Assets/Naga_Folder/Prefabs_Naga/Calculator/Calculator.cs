using UnityEngine;
using TMPro;
using System;
using System.Data;
namespace VerifyingOhmsLaw
{
    public class Calculator : MonoBehaviour
    {
        [Header("Display Reference")]
        [SerializeField] private TextMeshProUGUI displayScreen;

        private string visualExpression = "";
        private bool isResultState = false;

        private const int MAX_CHARS = 13;

        void Start()
        {
            ClearCalculator();
        }

        // ------------------------------------------------
        // NUMBER INPUT
        // ------------------------------------------------
        public void InputNumber(string value)
        {
            if (visualExpression.Length >= MAX_CHARS)
                return;

            if (isResultState)
            {
                visualExpression = "";
                isResultState = false;
            }

            visualExpression += value;
            UpdateDisplay();
        }

        // ------------------------------------------------
        // OPERATOR INPUT
        // ------------------------------------------------
        public void InputOperator(string value)
        {
            if (visualExpression.Length >= MAX_CHARS)
                return;

            isResultState = false;

            if (visualExpression.Length == 0 && value != "-") return;

            char lastChar = visualExpression[visualExpression.Length - 1];
            if (IsOperator(lastChar.ToString())) return;

            visualExpression += value;
            UpdateDisplay();
        }

        // ------------------------------------------------
        // BRACKET INPUT
        // ------------------------------------------------
        public void InputBracket(string bracket)
        {
            if (visualExpression.Length >= MAX_CHARS)
                return;

            if (isResultState)
            {
                visualExpression = "";
                isResultState = false;
            }

            if (bracket == "(")
            {
                if (visualExpression.Length > 0)
                {
                    char lastChar = visualExpression[visualExpression.Length - 1];
                    if (char.IsDigit(lastChar) || lastChar == ')')
                    {
                        if (visualExpression.Length + 1 >= MAX_CHARS) return;
                        visualExpression += "*";
                    }
                }

                visualExpression += "(";
            }
            else if (bracket == ")")
            {
                int openCount = visualExpression.Split('(').Length - 1;
                int closeCount = visualExpression.Split(')').Length - 1;

                if (openCount > closeCount)
                    visualExpression += ")";
            }

            UpdateDisplay();
        }

        // ------------------------------------------------
        // PERCENT %
        // ------------------------------------------------
        public void CalculatePercentage()
        {
            if (string.IsNullOrEmpty(visualExpression)) return;

            if (double.TryParse(visualExpression, out double num))
            {
                visualExpression = (num / 100f).ToString();
                isResultState = true;
                UpdateDisplay();
            }
        }

        // ------------------------------------------------
        // CLEAR
        // ------------------------------------------------
        public void ClearCalculator()
        {
            visualExpression = "";
            displayScreen.text = "0";
            isResultState = false;
        }

        // ------------------------------------------------
        // BACKSPACE
        // ------------------------------------------------
        public void Backspace()
        {
            if (isResultState)
            {
                ClearCalculator();
                return;
            }

            if (visualExpression.Length > 0)
            {
                visualExpression = visualExpression.Substring(0, visualExpression.Length - 1);
                UpdateDisplay();
            }
        }

        // ------------------------------------------------
        // +/- TOGGLE
        // ------------------------------------------------
        public void TogglePositiveNegative()
        {
            if (visualExpression.Length >= MAX_CHARS)
                return;

            if (double.TryParse(visualExpression, out double num))
            {
                visualExpression = (-num).ToString();
                UpdateDisplay();
            }
        }

        // ------------------------------------------------
        // CALCULATE =
        // ------------------------------------------------
        public void CalculateResult()
        {
            if (string.IsNullOrEmpty(visualExpression)) return;

            try
            {
                DataTable table = new DataTable();
                object result = table.Compute(visualExpression, "");

                visualExpression = Math.Round(Convert.ToDouble(result), 6).ToString();
                isResultState = true;
                UpdateDisplay();
            }
            catch
            {
                displayScreen.text = "Error";
                visualExpression = "";
            }
        }

        // ------------------------------------------------
        // HELPERS
        // ------------------------------------------------
        void UpdateDisplay()
        {
            displayScreen.text = string.IsNullOrEmpty(visualExpression) ? "0" : visualExpression;
        }

        bool IsOperator(string val)
        {
            return val == "+" || val == "-" || val == "*" || val == "/";
        }
    }
}