#!/bin/bash
#Convert [Visual Studio] markdown to HTML

#The first parameter will be inserted into the HTML as a title
TITLE="$1"

#Pull from STDIN
cat |

perl -pe '
#Change ___markdown-content___ to body
s/(body )?#___markdown-content___/body/g;

#Convert <hr>s
my $HR_SHORT="<img src=\"https://static.castledragmire.com/silksong/Line425.png\" align=top class=LineHR>";
my %m = (
	" ---"=> "<center>".$HR_SHORT."</center>",
	"---" => "\n" . $HR_SHORT,
	" ***"=> "\n" . "<img src=\"https://static.castledragmire.com/silksong/Line1230.png\" align=top class=LineHR>",
	"***" => "\n" . "<img src=\"https://static.castledragmire.com/silksong/Line1018.png\" align=top class=LineHR>",
);
s{^( ?(?:---|\*\*\*))\n}{$m{$1}."\n\n"}gme;

#All double spaces become SPACE+NBSP except inside html tags
s{(<[^>]*>)|(?<=\S)[ ]{2,}}{
	  defined $1 ? $1
	: (" \xC2\xA0" x (int(length($&)/2)).(length($&)%2 ? " " : ""))
}gex;

#Image align=middle to top
s{(<img\b[^>]*?)\balign="middle"}{$1 align="bottom"}gi;

' |

#Convert .md to .html
pandoc -f gfm+raw_html+strikeout --wrap=preserve -t html --quiet |

#Add the html title and the stylesheet in the head
{
	echo -e '<!DOCTYPE html>\n<html xmlns="http://www.w3.org/1999/xhtml">\n<head>\n\t<meta charset="UTF-8">\n\t<meta name="viewport" content="width=device-width, initial-scale=1">\n\t<title>'"$TITLE"'</title>'
	echo '<style>
body ul, body ol, body p { margin-top:0; }
html body ul, html body ol { padding-left:15px; }
body hr { margin-top:0; background-color:#d8dee4; border-color:#d8dee4; }
hr.small { height:1px; border:0; margin:0 0 1em 0; }
.LineHR { margin-top:6px; }
body br.Hide { display:initial; }
img { max-width:100%; height:auto; }
</style>
</head><body>'
	cat;
	echo -e '</body>\n</html>'
} |

perl -0777 -pe '
#Remove paragraphs on lines that have <center> or <right>
s{<p\b[^>]*>\s*((?:(?!</p\b).)*?<(?:center|right)\b.*?)(?:</p\s*>)}{$1}gis;

#Replace font-size: 1-6 with the appropriate size conversion
my %m = (
	6 => "xx-large",
	5 => "x-large",
	4 => "large",
	3 => "medium",
	2 => "small",
	1 => "x-small",
);
s{(<style\b[^>]*>)(.*?)(</style>)}{
	my ($Open, $CSS, $Close)=($1, $2, $3);
	$CSS =~ s/font-size:\s*([1-6])\s*;/"font-size: ".$m{$1}.";"/ge;
	$Open.$CSS.$Close
}gse;
' |

#Send to STDOUT
cat