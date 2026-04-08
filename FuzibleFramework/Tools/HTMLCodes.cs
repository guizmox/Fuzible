using System.Collections.Generic;

namespace SHSRFramework
{
    internal class HTMLCodes
    {
        public static List<string[]> GetHTMLCodes()
        {
            List<string[]> sHTMLList = new()
            {
                new string[] { "&#32;", " ", "Space                               " },
                new string[] { "&#33;", "!", "Exclamation mark                    " },
                new string[] { "&#34;", "\"", "Quotation mark                     " },
                new string[] { "&#35;", "#", "Pound / hash                        " },
                new string[] { "&#36;", "$", "Dollar sign                         " },
                new string[] { "&#37;", "%", "Percent sign                        " },
                new string[] { "&#38;", "&", "Ampersand                           " },
                new string[] { "&#39;", "`", "Apostrophe                          " },
                new string[] { "&#40;", "(", "Left bracket                        " },
                new string[] { "&#41;", ")", "Right bracket                       " },
                new string[] { "&#42;", "*", "Asterisk                            " },
                new string[] { "&#43;", "+", "Plus                                " },
                new string[] { "&#44;", ",", "Comma                               " },
                new string[] { "&#45;", "-", "Hyphen                              " },
                new string[] { "&#46;", ".", "Period                              " },
                new string[] { "&#47;", "/", "Forward Slash                       " },
                new string[] { "&#48;", "0", "Zero                                " },
                new string[] { "&#49;", "1", "One                                 " },
                new string[] { "&#50;", "2", "Two                                 " },
                new string[] { "&#51;", "3", "Three                               " },
                new string[] { "&#52;", "4", "Four                                " },
                new string[] { "&#53;", "5", "Five                                " },
                new string[] { "&#54;", "6", "Six                                 " },
                new string[] { "&#55;", "7", "Seven                               " },
                new string[] { "&#56;", "8", "Eight                               " },
                new string[] { "&#57;", "9", "Nine                                " },
                new string[] { "&#58;", ":", "Colon                               " },
                new string[] { "&#59;", "°", "Semicolon                           " },
                new string[] { "&#60;", "<", "Less than                           " },
                new string[] { "&#61;", "=", "Equals                              " },
                new string[] { "&#62;", ">", "Greater than                        " },
                new string[] { "&#63;", "?", "Question mark                       " },
                new string[] { "&#64;", "@", "\"At\" symbol                       " },
                new string[] { "&#65;", "A", "Upper case A                        " },
                new string[] { "&#66;", "B", "Upper case B                        " },
                new string[] { "&#67;", "C", "Upper case C                        " },
                new string[] { "&#68;", "D", "Upper case D                        " },
                new string[] { "&#69;", "E", "Upper case E                        " },
                new string[] { "&#70;", "F", "Upper case F                        " },
                new string[] { "&#71;", "G", "Upper case G                        " },
                new string[] { "&#72;", "H", "Upper case H                        " },
                new string[] { "&#73;", "I", "Upper case I                        " },
                new string[] { "&#74;", "J", "Upper case J                        " },
                new string[] { "&#75;", "K", "Upper case K                        " },
                new string[] { "&#76;", "L", "Upper case L                        " },
                new string[] { "&#77;", "M", "Upper case M                        " },
                new string[] { "&#78;", "N", "Upper case N                        " },
                new string[] { "&#79;", "O", "Upper case O                        " },
                new string[] { "&#80;", "P", "Upper case P                        " },
                new string[] { "&#81;", "Q", "Upper case Q                        " },
                new string[] { "&#82;", "R", "Upper case R                        " },
                new string[] { "&#83;", "S", "Upper case S                        " },
                new string[] { "&#84;", "T", "Upper case T                        " },
                new string[] { "&#85;", "U", "Upper case U                        " },
                new string[] { "&#86;", "V", "Upper case V                        " },
                new string[] { "&#87;", "W", "Upper case W                        " },
                new string[] { "&#88;", "X", "Upper case X                        " },
                new string[] { "&#89;", "Y", "Upper case Y                        " },
                new string[] { "&#90;", "Z", "Upper case Z                        " },
                new string[] { "&#91;", "[", "Left square bracket                 " },
                new string[] { "&#92;", "\\", "Backslash                            " },
                new string[] { "&#93;", "]", "Right square bracket                  " },
                new string[] { "&#94;", "^", "Caret                               " },
                new string[] { "&#95;", "_", "Underscore                          " },
                new string[] { "&#96;", "`", "Single quote / backtick             " },
                new string[] { "&#97;", "a", "Lower case a                        " },
                new string[] { "&#98;", "b", "Lower case b                        " },
                new string[] { "&#99;", "c", "Lower case c                        " },
                new string[] { "&#100;", "d", "Lower case d                       " },
                new string[] { "&#101;", "e", "Lower case e                       " },
                new string[] { "&#102;", "f", "Lower case f                       " },
                new string[] { "&#103;", "g", "Lower case g                       " },
                new string[] { "&#104;", "h", "Lower case h                       " },
                new string[] { "&#105;", "i", "Lower case i                       " },
                new string[] { "&#106;", "j", "Lower case j                       " },
                new string[] { "&#107;", "k", "Lower case k                       " },
                new string[] { "&#108;", "l", "Lower case l                       " },
                new string[] { "&#109;", "m", "Lower case m                       " },
                new string[] { "&#110;", "n", "Lower case n                       " },
                new string[] { "&#111;", "o", "Lower case o                       " },
                new string[] { "&#112;", "p", "Lower case p                       " },
                new string[] { "&#113;", "q", "Lower case q                       " },
                new string[] { "&#114;", "r", "Lower case r                       " },
                new string[] { "&#115;", "s", "Lower case s                       " },
                new string[] { "&#116;", "t", "Lower case t                       " },
                new string[] { "&#117;", "u", "Lower case u                       " },
                new string[] { "&#118;", "v", "Lower case v                       " },
                new string[] { "&#119;", "w", "Lower case w                       " },
                new string[] { "&#120;", "x", "Lower case x                       " },
                new string[] { "&#121;", "y", "Lower case y                       " },
                new string[] { "&#122;", "z", "Lower case z                       " },
                new string[] { "&#123;", "{", "Left curly brace                   " },
                new string[] { "&#124;", "|", "Pipe                               " },
                new string[] { "&#125;", "}", "Right curly brace                  " },
                new string[] { "&#126;", "~", "Tilde                              " },
                new string[] { "&#127;", "", "Delete                              " },
                new string[] { "&#160;", "", "Non-breaking space                  " },
                new string[] { "&#161;", "¡", "Inverted exclamation mark          " },
                new string[] { "&#162;", "", "                                    " },
                new string[] { "&#163;", "", "                                    " },
                new string[] { "&#164;", "", "                                    " },
                new string[] { "&#165;", "", "                                    " },
                new string[] { "&#166;", "", "                                    " },
                new string[] { "&#167;", "", "                                    " },
                new string[] { "&#168;", "", "                                    " },
                new string[] { "&#169;", "", "                                    " },
                new string[] { "&#170;", "", "                                    " },
                new string[] { "&#171;", "", "                                    " },
                new string[] { "&#172;", "", "                                    " },
                new string[] { "&#173;", "", "                                    " },
                new string[] { "&#174;", "", "                                    " },
                new string[] { "&#175;", "", "                                    " },
                new string[] { "&#176;", "", "                                    " },
                new string[] { "&#177;", "", "                                    " },
                new string[] { "&#178;", "", "                                    " },
                new string[] { "&#179;", "", "                                    " },
                new string[] { "&#180;", "", "                                    " },
                new string[] { "&#181;", "", "                                    " },
                new string[] { "&#182;", "", "                                    " },
                new string[] { "&#183;", "", "                                    " },
                new string[] { "&#184;", "", "                                    " },
                new string[] { "&#185;", "", "                                    " },
                new string[] { "&#186;", "", "                                    " },
                new string[] { "&#187;", "", "                                    " },
                new string[] { "&#188;", "", "                                    " },
                new string[] { "&#189;", "", "                                    " },
                new string[] { "&#190;", "¾", "Three quarters                     " },
                new string[] { "&#191;", "¿", "Inverted question mark             " },
                new string[] { "&#192;", "À", "A with grave                       " },
                new string[] { "&#193;", "Á", "A with acute                       " },
                new string[] { "&#194;", "Â", "A with circumflex                  " },
                new string[] { "&#195;", "Ã", "A with tilde                       " },
                new string[] { "&#196;", "Ä", "A with umlaut                      " },
                new string[] { "&#197;", "Å", "A with ring                        " },
                new string[] { "&#198;", "Æ", "AE                                 " },
                new string[] { "&#199;", "Ç", "C with cedilla                     " },
                new string[] { "&#200;", "È", "E with grave                       " },
                new string[] { "&#201;", "É", "E with acute                       " },
                new string[] { "&#202;", "Ê", "E with circumflex                  " },
                new string[] { "&#203;", "Ë", "E with umlaut                      " },
                new string[] { "&#204;", "Ì", "I with grave                       " },
                new string[] { "&#205;", "Í", "I with acute                       " },
                new string[] { "&#206;", "Î", "I with circumflex                  " },
                new string[] { "&#207;", "Ï", "I with umlaut                      " },
                new string[] { "&#208;", "Ð", "ETH                                " },
                new string[] { "&#209;", "Ñ", "N with tilde                       " },
                new string[] { "&#210;", "Ò", "O with grave                       " },
                new string[] { "&#211;", "Ó", "O with acute                       " },
                new string[] { "&#212;", "Ô", "O with circumflex                  " },
                new string[] { "&#213;", "Õ", "O with tilde                       " },
                new string[] { "&#214;", "Ö", "O with umlaut                      " },
                new string[] { "&#215;", "×", "Multiply symbol                    " },
                new string[] { "&#216;", "Ø", "O with slash                       " },
                new string[] { "&#217;", "Ù", "U with grave                       " },
                new string[] { "&#218;", "Ú", "U with acute                       " },
                new string[] { "&#219;", "Û", "U with circumflex                  " },
                new string[] { "&#220;", "Ü", "U with umlaut                      " },
                new string[] { "&#221;", "Ý", "Y with acute                       " },
                new string[] { "&#222;", "Þ", "THORN                                " },
                new string[] { "&#223;", "", "                                      " }
            };
            ;
            sHTMLList.Add(new string[] { "&#224;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#225;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#226;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#227;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#228;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#229;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#230;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#231;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#232;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#233;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#234;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#235;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#236;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#237;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#238;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#239;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#240;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#241;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#242;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#243;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#244;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#245;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#246;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#247;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#248;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#249;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#250;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#251;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#252;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#253;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#254;", "", "                                    " });
            sHTMLList.Add(new string[] { "&#255;", "", "                                    " });

            return sHTMLList;
        }
    }
}
