﻿//--------------------------------------------------------------------------------------------------
// <author>Jonathan Rann, Rob Gillen</author>
// <authorEmail>jcrann84@gmail.com, gillenre@ornl.gov</authorEmail>
// <remarks>
// </remarks>
// <copyright file="Program.cs" company="Oak Ridge National Laboratory">
//
//   Copyright (c) 2011 Oak Ridge National Laboratory, unless otherwise noted.  
//
//   Permission is hereby granted, free of charge, to any person obtaining a copy
//   of this software and associated documentation files (the "Software"), to deal
//   in the Software without restriction, including without limitation the rights
//   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//   copies of the Software, and to permit persons to whom the Software is
//   furnished to do so, subject to the following conditions:
//
//   The above copyright notice and this permission notice shall be included in all
//   copies or substantial portions of the Software.
//
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//   SOFTWARE.
//
// </copyright>
//--------------------------------------------------------------------------------------------------

namespace Ornl.Csmd.Csrg.Stahc.Core.Model
{
    /// <summary>
    /// 
    /// </summary>
    public class BlockTransferDetail
    {
        /// <summary>
        /// 
        /// </summary>
        public int StartPosition { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int BytesToRead { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string BlockId { get; set; }
    }
}