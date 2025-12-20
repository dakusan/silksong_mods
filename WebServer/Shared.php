<?php
function ErrAndDie($User, $Log=null)
{
	file_put_contents(__DIR__.'/errors.log', date('Y-m-d H:i:s ').($Log!=null ? "$User: $Log" : $User)."\n", FILE_APPEND|LOCK_EX);
	die($User);
}

require_once(__DIR__.'/Config.php');
try {
	$Conn=@new mysqli($Config->Host, $Config->User, $Config->Password, $Config->DBName);
	if($Conn->connect_errno)
		throw $mysqli->connect_error;
} catch(Exception $e) {
	ErrAndDie('SQL Connection Failed', $e->getMessage());
}
$Escape=fn($Str) => "'".$Conn->real_escape_string($Str)."'";

try {
	$Conn->query("SET NAMES 'utf8mb4' COLLATE 'utf8mb4_unicode_ci'");
	$Conn->query('SET time_zone='.$Escape(date_default_timezone_get()));
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

function Query($Str)
{
	global $Conn;
	return new MysqliResultIterator($Conn->query($Str));
}
?>