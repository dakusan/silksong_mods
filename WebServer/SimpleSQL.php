<?
require_once(__DIR__.'/Config.php');
require_once(__DIR__.'/Shared.php');

try {
	$Conn=@new mysqli($Config->Host, $Config->User, $Config->Password, $Config->DBName);
	if($Conn->connect_errno)
		throw $mysqli->connect_error;
} catch(Exception $e) {
	ErrAndDie('SQL Connection Failed', $e->getMessage());
}

try {
	$Conn->query("SET NAMES 'utf8mb4' COLLATE 'utf8mb4_unicode_ci'");
	$Conn->query('SET time_zone='.SQLEscape(date_default_timezone_get()));
} catch(Exception $e) {
	ErrAndDie('Mysql failed while initializing connection parameters', $e->getMessage());
}

class MysqliResultIterator implements Iterator {
	private mysqli_result	$Result;
	private mixed			$Current;
	private int				$Key=0;
	private bool			$Valid=false;
	public function __construct(mysqli_result $Result)			{ $this->Result=$Result; $this->rewind(); }
	public function current							(): mixed	{ return $this->Current;}
	public function key								(): int		{ return $this->Key;	}
	public function valid							(): bool	{ return $this->Valid;	}
	public function next							(): void	{ $this->Key+=($this->Valid=($this->Current=$this->Result->fetch_object())!==null ? 1 : 0); }
	public function rewind(): void {
		if($this->Key!==0)
			$this->Result->data_seek(0);
		$this->next();
	}
}

function Query(string $Query, mixed ...$Vars): MysqliResultIterator|int|null
{
	global $Conn;
	$QuerySections=explode('?', $Query);
	$EndQuery=array_shift($QuerySections);
	try {
		if(count($QuerySections)!=count($Vars))
			throw new Exception('Invalid number of vars');
		foreach($QuerySections as $Index => $QueryPart)
			$EndQuery.=FormatQueryItem($Conn, $Vars[$Index]).$QueryPart;
		$Result=$Conn->query($EndQuery);
	} catch(Exception $e) {
		ErrAndDie('Query error', "$Query\n$EndQuery\n$e\n".var_export($Vars, true));
	}

	if(preg_match('/^\s*(?:SELECT|SHOW)/i', $Query))
		return new MysqliResultIterator($Result);
	else if(preg_match('/^\s*INSERT/i', $Query))
		return $Conn->insert_id;
	return null;
}

function FormatQueryItem(mysqli $Conn, mixed $Item): string|int
{
	if($Item===null)
		return 'null';
	else if(is_int($Item))
		return (int)$Item;
	else if(is_float($Item))
		return (float)$Item;
	else if(is_bool($Item))
		return $Item ? 'true' : 'false';
	else
		return SQLEscape($Item);
}

function SQLEscape(string $Str): string
{
	global $Conn;
	return "'".$Conn->real_escape_string($Str)."'";
}
?>