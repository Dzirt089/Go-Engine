using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Корпус предложения мёртвых: 61 позиция, списки закреплены камень в камень.</summary>
/// <remarks>
/// <para>
/// Перебор мёртвых групп ускорен ради постоянной оценки позиции (D-070): он идёт на каждый ход,
/// и узел поиска подешевел в разы. Приговор от ускорения меняться не имеет права, а доказательство
/// ровно одно — совпадение с тем, что перебор предлагал до него.
/// </para>
/// <para>
/// Корпус: восемь расстановок правил (<see cref="TestPositions"/>), пять цумэго
/// (<see cref="TsumegoPositions"/>), 36 задач библиотеки и двенадцать контрольных точек реальной
/// партии 19×19 (эвристический самоиграй, зерно 20261002). Списки сняты до ускорения; партия
/// воспроизводится по записанным ходам, поэтому проверка не зависит от силы селектора.
/// </para>
/// <para>
/// Позиции сверяются по порядку, а не по имени: в цумэго дважды встречается одна и та же пара
/// «цвет + решающая точка», и имя ключом быть не может.
/// </para>
/// </remarks>
public sealed class DeadProposalCorpusTests
{
    /// <summary>Ожидаемые пометки позиций корпуса: имя для чтения и точки через запятую («-» — пусто).</summary>
    private static readonly (string Name, string Dead)[] ExpectedPositions =
    [
        ("Seki", "-"),
        ("BlockInAtari", "D4,E4,F4,D5,E5,F5,D6,E6,F6"),
        ("CapturingRace", "-"),
        ("SelfAtariPosition", "-"),
        ("SuicideAtCorner", "B1,A2"),
        ("SuicideWithCapture", "B1,A2,B2"),
        ("BoardWithoutLegalMoves", "-"),
        ("KoShape", "-"),
        ("Цумэго-Black-E5", "F1,G1,H1,J1,F2,G2,H2,J2,F3,G3,H3,J3,F4,G4,H4,J4,F5,G5,H5,J5,F6,G6,H6,J6,F7,G7,H7,J7,F8,G8,H8,J8,F9,G9,H9,J9"),
        ("Цумэго-Black-C5", "D1,E1,F1,G1,H1,J1,D2,E2,F2,G2,H2,J2,D3,E3,F3,G3,H3,J3,D4,E4,F4,G4,H4,J4,D5,E5,F5,G5,H5,J5,D6,E6,F6,G6,H6,J6,D7,E7,F7,G7,H7,J7,D8,E8,F8,G8,H8,J8,D9,E9,F9,G9,H9,J9"),
        ("Цумэго-Black-F5", "G1,H1,J1,G2,H2,J2,G3,H3,J3,G4,H4,J4,G5,H5,J5,G6,H6,J6,G7,H7,J7,G8,H8,J8,G9,H9,J9"),
        ("Цумэго-White-E5", "A1,B1,C1,D1,A2,B2,C2,D2,A3,B3,C3,D3,A4,B4,C4,D4,A5,B5,C5,D5,A6,B6,C6,D6,A7,B7,C7,D7,A8,B8,C8,D8,A9,B9,C9,D9"),
        ("Цумэго-Black-E5", "-"),
        ("Задача-ts-001", "B7,C7"),
        ("Задача-ts-002", "-"),
        ("Задача-ts-003", "-"),
        ("Задача-ts-004", "-"),
        ("Задача-ts-005", "-"),
        ("Задача-ts-006", "-"),
        ("Задача-ts-007", "-"),
        ("Задача-ts-008", "-"),
        ("Задача-ts-009", "-"),
        ("Задача-ts-010", "H7"),
        ("Задача-ts-011", "-"),
        ("Задача-ts-012", "-"),
        ("Задача-ts-013", "-"),
        ("Задача-ts-014", "-"),
        ("Задача-ts-015", "-"),
        ("Задача-ts-016", "-"),
        ("Задача-ts-017", "-"),
        ("Задача-ts-018", "-"),
        ("Задача-ts-019", "-"),
        ("Задача-ts-020", "-"),
        ("Задача-ts-021", "-"),
        ("Задача-ts-022", "-"),
        ("Задача-ts-023", "-"),
        ("Задача-ts-024", "-"),
        ("Задача-ts-025", "-"),
        ("Задача-ts-026", "D8"),
        ("Задача-ts-027", "-"),
        ("Задача-ts-028", "-"),
        ("Задача-ts-029", "-"),
        ("Задача-ts-030", "-"),
        ("Задача-ts-031", "-"),
        ("Задача-ts-032", "-"),
        ("Задача-ts-033", "-"),
        ("Задача-ts-034", "-"),
        ("Задача-ts-035", "-"),
        ("Задача-ts-036", "-"),
    ];

    /// <summary>Ожидаемые пометки на контрольных ходах партии 19×19: номер хода и список точек.</summary>
    private static readonly (int MoveNumber, string Dead)[] ExpectedGame =
    [
        (40, "-"),
        (80, "-"),
        (120, "T1"),
        (160, "T7"),
        (200, "E7,T7,B9"),
        (240, "E7,T7,B9,E14,F14,E15,E16"),
        (280, "B1,C1,D1,B2,P7,E14,F14,E15,E16,B17"),
        (320, "H1,L7,B16,B17,D18,O18,P18"),
        (360, "F1,H1,F2,G2,Q2,G3,H3,L4,M4,K5,L5,M5,N5,K6,K13,E14,F14,G16,T16,G17,T17,D18"),
        (400, "B1,C1,B2,F2,G2,Q2,G3,K5,C6,H10,Q10,H11,G16,G17,D18"),
        (440, "F14,K14,B17,G17,D18"),
        (467, "-"),
    ];

    /// <summary>Ходы партии 19×19 по два символа на ход; «--» — пас.</summary>
    private const string GameMoves = "fa,dd,sm,pd,pp,lf,qs,fr,og,kp,np,cb,aq,gm,sk,il,pc,po,di,ks,kl,bl,oj,rb,qi,qk,ro,mg,bp,ed,dk,jd,nj,fq,nl,lb,id,os,ol,dm,aj,pf,ce,kk,le,lc,je,do,jb,pj,ni,dn,ob,or,ak,ne,re,io,cl,ck,rg,mc,qp,mn,qo,mi,bj,sa,pm,of,ok,mb,fn,if,hs,ic,rf,fe,rk,cn,lr,pi,fg,mf,hl,qj,cj,bk,qa,on,ki,ec,ee,cd,ms,sp,an,lo,gb,dq,de,nb,ns,ah,kg,fd,nq,ae,gp,cf,rc,eg,oo,pn,jm,ag,ke,gl,rh,cc,od,sg,qr,qn,hn,li,bb,ln,oe,bo,sd,fo,ha,as,bg,rp,rm,ab,bq,kh,ra,sb,eo,ji,om,kj,pk,hi,dg,la,hc,go,fb,jc,be,rj,ld,jh,rd,rs,oq,mh,me,nm,en,bf,ch,cp,ca,qh,ri,mr,mo,cg,bh,lg,ao,ka,ei,hr,kn,rq,mm,nn,gj,am,df,af,ph,bi,qg,bn,pr,jj,ap,kb,da,hd,ff,ie,js,fm,cr,ko,qq,eb,in,fp,hj,gf,lp,hh,lj,hm,hk,gi,ba,dl,cm,bm,sn,qe,km,cs,ep,jk,rl,gs,is,gn,si,cq,ho,ek,dj,pa,gc,qb,lq,eq,kr,ar,ls,br,dr,ac,pb,oh,kd,al,gg,er,ds,fc,bs,im,cs,jp,es,fs,he,nf,jr,na,pl,db,qm,oa,jf,eh,bq,fj,ps,nr,mq,ea,sf,pg,sh,aa,bc,bd,ng,nh,kq,iq,ml,ig,qh,ia,mk,nd,gk,jl,fk,ib,gh,ja,dr,qc,rr,oc,ss,or,ql,pe,so,sc,jn,sq,sr,dp,em,el,bp,ii,ir,lm,ik,nr,gq,qf,mr,hp,os,jg,qd,kf,fh,hf,ge,gd,rn,sq,ej,ao,mj,fi,oi,ll,sj,jo,ef,ij,dh,fl,hk,ap,jq,aq,en,lk,pb,ik,fn,ai,jm,bs,rq,sp,rp,in,no,hb,jn,ga,cr,km,gc,hg,ci,kn,ad,ae,af,em,ds,eo,hj,ad,fb,gk,ba,cs,ca,es,bb,md,gb,kc,je,ob,dr,cf,ld,pj,rj,le,kd,ke,pi,hl,qj,gj,cg,pc,bf,jf,fa,hc,fb,gc,gr,hq,gq,gp,hj,ds,hn,ai,bi,hk,ah,ho,ng,fa,fn,da,bq,bc,ba,gb,bb,ca,cl,og,jn,cm,kd,ld,ge,he,ra,en,bb,qa,od,ba,--,cr,--,oe,--,jm,--,bp,--,gr,--,sp,sq,eg,eh,nr,or,--,--";

    [Fact]
    public void Предложение_Совпадает_С_Корпусом_Позиций()
    {
        var boards = Positions();

        Assert.Equal(ExpectedPositions.Length, boards.Count);

        for (var index = 0; index < boards.Count; index++)
        {
            Assert.Equal(ExpectedPositions[index].Dead, DeadList(Endgame.ProposeDead(boards[index])));
        }
    }

    [Fact]
    public void Предложение_Совпадает_С_Партией_19x19()
    {
        var game = GameState.NewGame(BoardSize.Size19, Komi.For19x19);
        var played = 0;
        var checkedPoints = 0;

        foreach (var move in GameMoves.Split(','))
        {
            var playedMove = move == "--"
                ? Move.Pass(game.ToMove)
                : Move.Play(new Point((byte)(move[0] - 'a'), (byte)(move[1] - 'a')), game.ToMove);

            _ = game.Play(playedMove);

            played++;

            foreach (var (moveNumber, dead) in ExpectedGame)
            {
                if (moveNumber != played)
                {
                    continue;
                }

                checkedPoints++;

                Assert.Equal(dead, DeadList(Endgame.ProposeDead(game.Board)));
            }
        }

        Assert.Equal(ExpectedGame.Length, checkedPoints);
    }

    /// <summary>Возвращает список помеченных точек строкой: «A9,B9» или «-».</summary>
    /// <param name="dead">Помеченные точки в порядке обхода доски.</param>
    /// <returns>Строка для сравнения с закреплённым списком.</returns>
    private static string DeadList(IReadOnlyList<Point> dead) =>
        dead.Count == 0 ? "-" : string.Join(",", dead.Select(point => point.ToString()));

    /// <summary>Собирает позиции корпуса в том порядке, в каком сняты ожидаемые списки.</summary>
    /// <returns>Восемь расстановок правил, пять цумэго и 36 задач библиотеки.</returns>
    private static List<Board> Positions()
    {
        List<Board> boards =
        [
            TestPositions.Seki(),
            TestPositions.BlockInAtari(),
            TestPositions.CapturingRace(),
            TestPositions.SelfAtariPosition(),
            TestPositions.SuicideAtCorner(),
            TestPositions.SuicideWithCapture(),
            TestPositions.BoardWithoutLegalMoves(),
            TestPositions.KoShape(null)
        ];

        foreach (var (rows, _, _) in TsumegoPositions.All)
        {
            boards.Add(TsumegoBuilder.Build(rows));
        }

        foreach (var problem in ProblemLibrary.All)
        {
            var board = ProblemSetup.Build(problem.Size, problem.Stones);

            if (board.IsSuccess)
            {
                boards.Add(board.Value!);
            }
        }

        return boards;
    }
}
