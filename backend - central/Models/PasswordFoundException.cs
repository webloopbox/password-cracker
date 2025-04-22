using System;

namespace backend___central.Models {
    public class PasswordFoundException : Exception
    {
        public PasswordFoundException(string message) : base(message) { }
    }
}